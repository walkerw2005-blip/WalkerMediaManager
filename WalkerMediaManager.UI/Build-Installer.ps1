$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build-Portable.ps1')

$possibleCompilers = @(
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $possibleCompilers | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Host ''
    Write-Host 'The portable app was built successfully.' -ForegroundColor Green
    Write-Host 'Inno Setup 6 is required to create the installer EXE.' -ForegroundColor Yellow
    Write-Host 'Install Inno Setup 6, then run Build-Installer.cmd again.' -ForegroundColor Yellow
    exit 2
}

$script = Join-Path $PSScriptRoot 'Installer\WalkerMediaManager.iss'
& $iscc $script
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }

$setup = Join-Path $PSScriptRoot 'Installer\Output\WalkerMediaManager-Setup-1.0.0-RC1.exe'
if (-not (Test-Path $setup)) { throw 'Installer compilation completed, but the setup EXE was not found.' }
Write-Host ''
Write-Host 'Installer created:' -ForegroundColor Green
Write-Host $setup
Start-Process explorer.exe -ArgumentList "/select,`"$setup`""
