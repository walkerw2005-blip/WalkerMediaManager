[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$dataFolder = Join-Path $env:LOCALAPPDATA 'WalkerMediaManager'
$databasePath = Join-Path $dataFolder 'walker.db'
$backupFolder = Join-Path $dataFolder 'Backups'

if (-not (Test-Path $databasePath)) {
    throw "Walker Media Manager database not found at $databasePath"
}

New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
$timestamp = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$backupPath = Join-Path $backupFolder "walker_before_v1.0.1_$timestamp.db"
Copy-Item -LiteralPath $databasePath -Destination $backupPath -Force

Write-Host "Database backup created:" -ForegroundColor Green
Write-Host $backupPath
