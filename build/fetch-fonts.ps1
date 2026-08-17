# Requires Windows PowerShell 5.1. Fetches SIL OFL font files into assets/fonts.
# Do not use pwsh. Do not leave release zips in the repository.

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$RepoRoot = Split-Path -Parent $PSScriptRoot
$CascadiaDir = Join-Path $RepoRoot "assets\fonts\cascadia"
$SourceHanDir = Join-Path $RepoRoot "assets\fonts\source-han-sans"
$TempRoot = Join-Path $env:TEMP "WindowsIDE-fonts"

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Test-ExistingFont {
    param(
        [string]$Path,
        [int]$MinimumBytes
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }
    $item = Get-Item -LiteralPath $Path
    return ($item.Length -ge $MinimumBytes)
}

function Get-OfficialFile {
    param(
        [string[]]$Urls,
        [string]$OutFile,
        [int]$MinimumBytes
    )
    if (Test-ExistingFont -Path $OutFile -MinimumBytes $MinimumBytes) {
        Write-Host ("Already present: " + $OutFile)
        return
    }

    Ensure-Directory (Split-Path -Parent $OutFile)
    $lastError = $null
    foreach ($url in $Urls) {
        $tmp = $OutFile + ".download"
        try {
            Write-Host ("GET " + $url)
            Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing
            $len = (Get-Item -LiteralPath $tmp).Length
            if ($len -lt $MinimumBytes) {
                throw ("Downloaded file too small (" + $len + " bytes): " + $url)
            }
            Move-Item -LiteralPath $tmp -Destination $OutFile -Force
            Write-Host ("Saved " + $OutFile + " (" + $len + " bytes)")
            return
        }
        catch {
            $lastError = $_
            if (Test-Path -LiteralPath $tmp) {
                Remove-Item -LiteralPath $tmp -Force
            }
            Write-Host ("Failed: " + $url + " :: " + $_.Exception.Message)
        }
    }
    throw ("Could not download " + $OutFile + ". Last error: " + $lastError)
}

function Get-ZipEntries {
    param(
        [string]$ZipUrl,
        [hashtable]$Entries,
        [int]$MinimumBytes
    )
    $needed = @{}
    foreach ($key in $Entries.Keys) {
        $outFile = [string]$Entries[$key]
        if (-not (Test-ExistingFont -Path $outFile -MinimumBytes $MinimumBytes)) {
            $needed[$key] = $outFile
        }
        else {
            Write-Host ("Already present: " + $outFile)
        }
    }
    if ($needed.Count -eq 0) {
        return
    }

    Ensure-Directory $TempRoot
    foreach ($outFile in $needed.Values) {
        Ensure-Directory (Split-Path -Parent $outFile)
    }

    $zipPath = Join-Path $TempRoot ([Guid]::NewGuid().ToString() + ".zip")
    try {
        Write-Host ("GET zip " + $ZipUrl)
        Invoke-WebRequest -Uri $ZipUrl -OutFile $zipPath -UseBasicParsing
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            foreach ($entryPath in $needed.Keys) {
                $outFile = [string]$needed[$entryPath]
                $wanted = $entryPath.Replace("/", "\")
                $entry = $null
                foreach ($candidate in $zip.Entries) {
                    $name = $candidate.FullName.Replace("/", "\")
                    if ($name -eq $wanted -or $name.EndsWith("\" + $wanted)) {
                        $entry = $candidate
                        break
                    }
                }
                if ($null -eq $entry) {
                    throw ("Zip entry not found: " + $entryPath)
                }
                if (Test-Path -LiteralPath $outFile) {
                    Remove-Item -LiteralPath $outFile -Force
                }
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $outFile, $true)
                $len = (Get-Item -LiteralPath $outFile).Length
                if ($len -lt $MinimumBytes) {
                    throw ("Extracted file too small (" + $len + " bytes): " + $entryPath)
                }
                Write-Host ("Extracted " + $outFile + " (" + $len + " bytes)")
            }
        }
        finally {
            $zip.Dispose()
        }
    }
    finally {
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }
    }
}

Ensure-Directory $CascadiaDir
Ensure-Directory $SourceHanDir

Get-OfficialFile -Urls @(
    "https://raw.githubusercontent.com/microsoft/cascadia-code/v2407.24/LICENSE"
) -OutFile (Join-Path $CascadiaDir "LICENSE") -MinimumBytes 200

Get-OfficialFile -Urls @(
    "https://raw.githubusercontent.com/adobe-fonts/source-han-sans/2.005R/LICENSE.txt",
    "https://raw.githubusercontent.com/adobe-fonts/source-han-sans/release/LICENSE.txt"
) -OutFile (Join-Path $SourceHanDir "LICENSE.txt") -MinimumBytes 200

$cascadiaRegular = Join-Path $CascadiaDir "CascadiaMono-Regular.ttf"
$cascadiaBold = Join-Path $CascadiaDir "CascadiaMono-Bold.ttf"
$sourceHan = Join-Path $SourceHanDir "SourceHanSansJP-Regular.otf"

Get-OfficialFile -Urls @(
    "https://github.com/adobe-fonts/source-han-sans/raw/2.005R/SubsetOTF/JP/SourceHanSansJP-Regular.otf",
    "https://github.com/adobe-fonts/source-han-sans/raw/release/SubsetOTF/JP/SourceHanSansJP-Regular.otf"
) -OutFile $sourceHan -MinimumBytes 1000000

$cascadiaZip = "https://github.com/microsoft/cascadia-code/releases/download/v2407.24/CascadiaCode-2407.24.zip"
$cascadiaEntries = @{}
$cascadiaEntries["ttf\static\CascadiaMono-Regular.ttf"] = $cascadiaRegular
$cascadiaEntries["ttf\static\CascadiaMono-Bold.ttf"] = $cascadiaBold
Get-ZipEntries -ZipUrl $cascadiaZip -Entries $cascadiaEntries -MinimumBytes 100000

Write-Host "Fonts ready."
Write-Host ("  " + $cascadiaRegular)
Write-Host ("  " + $cascadiaBold)
Write-Host ("  " + $sourceHan)
