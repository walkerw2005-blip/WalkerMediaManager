$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'WalkerMediaManager.UI.csproj'
$publishDir = Join-Path $PSScriptRoot 'artifacts\publish\win-x64'
$exe = Join-Path $publishDir 'WalkerMediaManager.exe'

Write-Host 'Building Walker Media Manager 1.0.0 portable folder...' -ForegroundColor Cyan

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK was not found. Install the .NET 8 SDK or the Visual Studio .NET desktop development workload.'
}

$running = Get-Process -Name 'WalkerMediaManager' -ErrorAction SilentlyContinue
if ($running) {
    throw 'Walker Media Manager is still running. Close it completely, then run Build-Portable.cmd again.'
}

if (Test-Path $publishDir) {
    Write-Host 'Removing the previous portable build...' -ForegroundColor DarkGray
    Remove-Item $publishDir -Recurse -Force
}

Write-Host 'Restoring packages...' -ForegroundColor DarkGray
dotnet restore $project
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

Write-Host 'Publishing the complete WinUI application folder...' -ForegroundColor DarkGray
dotnet publish $project `
    -c Release `
    -p:Platform=x64 `
    -p:PublishProfile=win-x64-portable
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

if (-not (Test-Path $exe)) {
    throw "Publish finished, but $exe was not found."
}

$buildInfo = @"
Walker Media Manager portable build
Built: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Executable: WalkerMediaManager.exe

IMPORTANT: Keep every file in this folder together. This WinUI application is
portable as a folder, not as a standalone single EXE.
"@
Set-Content -Path (Join-Path $publishDir 'PORTABLE-BUILD.txt') -Value $buildInfo -Encoding UTF8

Write-Host ''
Write-Host 'Portable build completed successfully.' -ForegroundColor Green
Write-Host 'Run the EXE from this folder:' -ForegroundColor Green
Write-Host $exe
Write-Host ''
Write-Host 'Do not copy only WalkerMediaManager.exe; keep the whole folder together.' -ForegroundColor Yellow

Start-Process explorer.exe -ArgumentList "/select,`"$exe`""
