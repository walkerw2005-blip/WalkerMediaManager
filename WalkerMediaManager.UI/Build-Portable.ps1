$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'WalkerMediaManager.UI.csproj'
$publishDir = Join-Path $PSScriptRoot 'artifacts\publish\win-x64'

Write-Host 'Building Walker Media Manager v1.0.0 RC1...' -ForegroundColor Cyan
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK was not found. Install the .NET 8 SDK or the Visual Studio .NET desktop development workload.'
}

dotnet restore $project
dotnet publish $project -c Release -p:Platform=x64 -p:PublishProfile=win-x64-installer
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$exe = Join-Path $publishDir 'WalkerMediaManager.exe'
if (-not (Test-Path $exe)) { throw "Publish finished, but $exe was not found." }

Write-Host ''
Write-Host 'Portable build created:' -ForegroundColor Green
Write-Host $exe
Write-Host 'You can double-click this EXE now.' -ForegroundColor Green
Start-Process explorer.exe -ArgumentList "/select,`"$exe`""
