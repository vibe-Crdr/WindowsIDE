# 指定 Framework フォルダの mscorlib / System が欠如せず 4.8 ファミリーであることを検査する。
function Assert-FrameworkBclFamily {
    param([string]$FrameworkDirectory)
    foreach ($name in @("mscorlib.dll", "System.dll")) {
        $path = Join-Path $FrameworkDirectory $name
        if (-not (Test-Path -LiteralPath $path)) {
            Write-Host ("BCL missing: " + $path)
            exit 1
        }
        $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($path)
        $fv = $info.FileVersion
        Write-Host ("BCL " + $name + " FileVersion=" + $fv)
        if ($info.FileMajorPart -ne 4 -or $info.FileMinorPart -ne 8) {
            Write-Host ("BCL family mismatch for " + $path + ": expected 4.8.x, got " + $fv)
            exit 1
        }
    }
}
