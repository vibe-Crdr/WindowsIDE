# Requires Windows PowerShell 5.1. Do not use pwsh.
$ErrorActionPreference = "Stop"

$Csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

if (-not (Test-Path -LiteralPath $Csc)) {
    Write-Host ("csc not found: " + $Csc)
    exit 1
}

function Add-Utf8Bom {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return
    }
    $bom = [byte[]](0xEF, 0xBB, 0xBF)
    $newBytes = New-Object byte[] ($bytes.Length + 3)
    [Array]::Copy($bom, 0, $newBytes, 0, 3)
    [Array]::Copy($bytes, 0, $newBytes, 3, $bytes.Length)
    [System.IO.File]::WriteAllBytes($Path, $newBytes)
}

Get-ChildItem -LiteralPath (Join-Path $RepoRoot "src") -Filter *.cs -Recurse | ForEach-Object {
    Add-Utf8Bom $_.FullName
}
Get-ChildItem -LiteralPath (Join-Path $RepoRoot "tests") -Filter *.cs -Recurse | ForEach-Object {
    Add-Utf8Bom $_.FullName
}

$outDir = Join-Path $RepoRoot "build\out"
if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$rsp = "build\windows-ide-tests.rsp"
Write-Host "Compiling tests with $rsp"
& $Csc /noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) {
    Write-Host ("csc tests failed with exit " + $LASTEXITCODE)
    exit $LASTEXITCODE
}

Write-Host "compile-tests.ps1 OK"
