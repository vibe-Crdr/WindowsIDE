# Requires Windows PowerShell 5.1. Do not use pwsh.
# Cursor hook: mark codebase-memory stale after edits, then ask the agent
# to call index_repository once when the agent loop stops.
$ErrorActionPreference = "Stop"

$StateDir = Join-Path $PSScriptRoot "state"
$StatePath = Join-Path $StateDir "cbm-index-dirty.json"
$MaxSamplePaths = 12

function Write-StdoutJson {
    param([string]$Text)
    $utf8 = New-Object System.Text.UTF8Encoding $false
    $bytes = $utf8.GetBytes($Text)
    $stdout = [Console]::OpenStandardOutput()
    $stdout.Write($bytes, 0, $bytes.Length)
}

function Write-EmptyHookOutput {
    Write-StdoutJson "{}"
}

function Read-HookInput {
    $utf8 = New-Object System.Text.UTF8Encoding $false
    $stdin = New-Object System.IO.StreamReader([Console]::OpenStandardInput(), $utf8)
    $raw = $stdin.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $null
    }
    return $raw | ConvertFrom-Json
}

function Get-PropertyValue {
    param(
        $Object,
        [string]$Name
    )
    if ($null -eq $Object) {
        return $null
    }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) {
        return $null
    }
    return $prop.Value
}

function Get-WorkspaceRoot {
    param($Payload)
    # Prefer the script location so CJK repo paths do not depend on stdin encoding.
    $fromScript = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
    if (Test-Path -LiteralPath $fromScript) {
        return $fromScript
    }
    $roots = Get-PropertyValue -Object $Payload -Name "workspace_roots"
    if ($null -ne $roots) {
        if ($roots -is [System.Array] -and $roots.Length -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$roots[0])) {
            return [System.IO.Path]::GetFullPath([string]$roots[0])
        }
        $one = [string]$roots
        if (-not [string]::IsNullOrWhiteSpace($one)) {
            return [System.IO.Path]::GetFullPath($one)
        }
    }
    return $fromScript
}

function ConvertTo-JsonString {
    param([string]$Value)
    if ($null -eq $Value) {
        return ""
    }
    $builder = New-Object System.Text.StringBuilder
    foreach ($ch in $Value.ToCharArray()) {
        $code = [int]$ch
        if ($ch -eq [char]34) {
            [void]$builder.Append("\`"")
        } elseif ($ch -eq [char]92) {
            [void]$builder.Append("\\")
        } elseif ($code -eq 10) {
            [void]$builder.Append("\n")
        } elseif ($code -eq 13) {
            [void]$builder.Append("\r")
        } elseif ($code -eq 9) {
            [void]$builder.Append("\t")
        } elseif ($code -lt 32) {
            [void]$builder.Append(("\u{0:x4}" -f $code))
        } else {
            [void]$builder.Append($ch)
        }
    }
    return $builder.ToString()
}

function ConvertTo-RepoRelativePath {
    param(
        [string]$FilePath,
        [string]$Root
    )
    $full = [System.IO.Path]::GetFullPath($FilePath)
    $rootFull = $Root.TrimEnd("\", "/")
    $prefix = $rootFull + [System.IO.Path]::DirectorySeparatorChar
    if ($full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $rel = $full.Substring($prefix.Length)
        return ($rel -replace "\\", "/")
    }
    if ($full.Equals($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return "."
    }
    return $null
}

function Test-ShouldMarkDirty {
    param([string]$RelativePath)
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or $RelativePath -eq ".") {
        return $false
    }
    $lower = $RelativePath.ToLowerInvariant()
    $skipPrefixes = @(
        ".cursor/hooks/state/",
        ".cursor/memory/",
        ".cursor/plans/",
        "build/out/",
        ".git/",
        ".codebase-memory/"
    )
    foreach ($prefix in $skipPrefixes) {
        if ($lower.StartsWith($prefix)) {
            return $false
        }
    }
    if ($lower -match "(^|/)\.git/") {
        return $false
    }
    if ($lower -match "\.(ttf|otf|exe|pdb|dll|zip|db|db-wal|db-shm)$") {
        return $false
    }
    return $true
}

function Read-DirtyState {
    if (-not (Test-Path -LiteralPath $StatePath)) {
        return $null
    }
    try {
        return (Get-Content -LiteralPath $StatePath -Raw -Encoding UTF8) | ConvertFrom-Json
    } catch {
        return $null
    }
}

function Save-DirtyState {
    param(
        [string[]]$Paths
    )
    if (-not (Test-Path -LiteralPath $StateDir)) {
        New-Item -ItemType Directory -Path $StateDir | Out-Null
    }
    $unique = @()
    foreach ($path in $Paths) {
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }
        $found = $false
        foreach ($existing in $unique) {
            if ($existing.Equals($path, [System.StringComparison]::OrdinalIgnoreCase)) {
                $found = $true
                break
            }
        }
        if (-not $found) {
            $unique += $path
        }
        if ($unique.Count -ge $MaxSamplePaths) {
            break
        }
    }
    $payload = @{
        version = 1
        dirty = $true
        paths = $unique
        updatedAt = [DateTime]::UtcNow.ToString("o")
    }
    $json = $payload | ConvertTo-Json -Compress
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($StatePath, $json, $utf8)
}

function Clear-DirtyState {
    if (Test-Path -LiteralPath $StatePath) {
        Remove-Item -LiteralPath $StatePath -Force
    }
}

function Invoke-MarkDirty {
    param($Payload)
    $root = Get-WorkspaceRoot -Payload $Payload
    $filePath = [string](Get-PropertyValue -Object $Payload -Name "file_path")
    if ([string]::IsNullOrWhiteSpace($filePath)) {
        Write-EmptyHookOutput
        return
    }
    $relative = ConvertTo-RepoRelativePath -FilePath $filePath -Root $root
    if ($null -eq $relative -or -not (Test-ShouldMarkDirty -RelativePath $relative)) {
        Write-EmptyHookOutput
        return
    }
    $paths = @($relative)
    $existing = Read-DirtyState
    if ($null -ne $existing) {
        $oldPaths = Get-PropertyValue -Object $existing -Name "paths"
        if ($null -ne $oldPaths) {
            foreach ($old in @($oldPaths)) {
                $paths += [string]$old
            }
        }
    }
    Save-DirtyState -Paths $paths
    Write-EmptyHookOutput
}

function Invoke-StopFollowup {
    param($Payload)
    $status = [string](Get-PropertyValue -Object $Payload -Name "status")
    $loopCount = Get-PropertyValue -Object $Payload -Name "loop_count"
    $loopValue = 0
    if ($null -ne $loopCount) {
        $loopValue = [int]$loopCount
    }
    if ($status -ne "completed" -or $loopValue -ne 0) {
        Write-EmptyHookOutput
        return
    }
    $existing = Read-DirtyState
    $isDirty = $false
    if ($null -ne $existing) {
        $flag = Get-PropertyValue -Object $existing -Name "dirty"
        if ($true -eq $flag) {
            $isDirty = $true
        }
    }
    if (-not $isDirty) {
        Write-EmptyHookOutput
        return
    }

    $samples = @()
    $oldPaths = Get-PropertyValue -Object $existing -Name "paths"
    if ($null -ne $oldPaths) {
        foreach ($old in @($oldPaths)) {
            $text = [string]$old
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                $samples += $text
            }
        }
    }
    $sampleText = ""
    if ($samples.Count -gt 0) {
        $sampleText = " Edited examples: " + ($samples -join ", ") + "."
    }

    $message = "WindowsIDE files were edited and the codebase-memory graph is stale." + $sampleText + " Call GetMcpTools then CallMcpTool on the codebase-memory server, tool index_repository. Use this workspace root as repo_path (the directory that contains AGENTS.md and src/WindowsIDE), name 'WindowsIDE', mode 'full'. Do not index Auto_DayTrador or a LOCALAPPDATA cbm-session placeholder. After success, report node and edge counts and stop. Do not start new implementation work."

    Clear-DirtyState

    $escaped = ConvertTo-JsonString -Value $message
    Write-StdoutJson ("{`"followup_message`":`"" + $escaped + "`"}")
}

try {
    $payload = Read-HookInput
    if ($null -eq $payload) {
        Write-EmptyHookOutput
        exit 0
    }

    $eventName = [string](Get-PropertyValue -Object $payload -Name "hook_event_name")
    $eventLower = $eventName.ToLowerInvariant()
    $hasFile = $null -ne (Get-PropertyValue -Object $payload -Name "file_path")
    $hasStatus = $null -ne (Get-PropertyValue -Object $payload -Name "status")

    if ($eventLower -eq "stop" -or ($hasStatus -and -not $hasFile)) {
        Invoke-StopFollowup -Payload $payload
    } else {
        Invoke-MarkDirty -Payload $payload
    }
    exit 0
} catch {
    try {
        [Console]::Error.WriteLine("[cbm-index-hook] " + $_.Exception.Message)
    } catch {
    }
    Write-EmptyHookOutput
    exit 0
}
