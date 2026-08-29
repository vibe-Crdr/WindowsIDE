# Requires Windows PowerShell 5.1. Do not use pwsh.
$ErrorActionPreference = "Stop"

$Csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$Fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

function Fail {
    param([string]$Message)
    Write-Host $Message
    exit 1
}

if (-not (Test-Path -LiteralPath $Csc)) {
    Fail ("csc not found: " + $Csc)
}

Write-Host "csc: $Csc"
$helpFile = Join-Path $env:TEMP "windows-ide-csc-help.txt"
& $Csc /help 1> $helpFile 2>&1
$banner = Get-Content -LiteralPath $helpFile -TotalCount 6
foreach ($line in $banner) {
    Write-Host $line
}
$bannerText = ($banner -join " ")
if ($bannerText -notlike "*for C# 5*") {
    Fail "csc banner is not Framework C# 5."
}

. (Join-Path $PSScriptRoot "framework-bcl.ps1")
Assert-FrameworkBclFamily -FrameworkDirectory $Fw

$fontRegular = Join-Path $RepoRoot "assets\fonts\cascadia\CascadiaMono-Regular.ttf"
$fontBold = Join-Path $RepoRoot "assets\fonts\cascadia\CascadiaMono-Bold.ttf"
$fontSource = Join-Path $RepoRoot "assets\fonts\source-han-sans\SourceHanSansJP-Regular.otf"
if (-not (Test-Path -LiteralPath $fontRegular) -or -not (Test-Path -LiteralPath $fontBold) -or -not (Test-Path -LiteralPath $fontSource)) {
    Fail "Bundled fonts missing. Run build\fetch-fonts.ps1"
}

$codeFonts = Get-ChildItem -LiteralPath (Join-Path $RepoRoot "assets\fonts") -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -like "CascadiaCode*"
}
if ($codeFonts) {
    Fail ("Cascadia Code must not be in the repo: " + ($codeFonts | ForEach-Object { $_.FullName }))
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

$outDir = Join-Path $RepoRoot "build\out"
if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$rsp = "build\windows-ide.rsp"
Write-Host "Compiling with $rsp"
& $Csc /noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) {
    Fail ("csc failed with exit " + $LASTEXITCODE)
}

$exe = Join-Path $outDir "WindowsIDE.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    Fail "WindowsIDE.exe was not produced."
}

$configSrc = Join-Path $RepoRoot "src\WindowsIDE\app.config"
Copy-Item -LiteralPath $configSrc -Destination (Join-Path $outDir "WindowsIDE.exe.config") -Force

function Test-X64Pe {
    param([string]$Path)
    $fs = [System.IO.File]::OpenRead($Path)
    try {
        $br = New-Object System.IO.BinaryReader($fs)
        $fs.Seek(0x3C, "Begin") | Out-Null
        $peOff = $br.ReadInt32()
        $fs.Seek($peOff, "Begin") | Out-Null
        $sig = $br.ReadUInt32()
        if ($sig -ne 0x4550) {
            Fail "Not a PE file."
        }
        $machine = $br.ReadUInt16()
        if ($machine -ne 0x8664) {
            Fail ("PE Machine is 0x{0:X4}, expected AMD64 0x8664" -f $machine)
        }
        Write-Host "PE Machine=AMD64 (x64)"
    }
    finally {
        $fs.Dispose()
    }
}

Test-X64Pe -Path $exe

$dlls = @(Get-ChildItem -LiteralPath $outDir -Filter *.dll -ErrorAction SilentlyContinue)
if ($dlls.Count -gt 0) {
    Fail ("Unexpected DLL in output: " + ($dlls | ForEach-Object { $_.Name }))
}

$raw = [System.IO.File]::ReadAllBytes($exe)
$asm = [System.Reflection.Assembly]::Load($raw)
$names = $asm.GetManifestResourceNames()
Write-Host ("Resources: " + ($names -join ", "))
$required = @(
    "WindowsIDE.Fonts.CascadiaMonoRegular",
    "WindowsIDE.Fonts.CascadiaMonoBold",
    "WindowsIDE.Fonts.SourceHanSansJpRegular"
)
foreach ($name in $required) {
    if ($names -notcontains $name) {
        Fail ("Missing embedded resource: " + $name)
    }
}

Write-Host "compile.ps1 OK: $exe"
