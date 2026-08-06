[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$cerPath = Join-Path $projectRoot "ReleaseCertificate\WalkerMediaManager-v1.0.cer"
$packageDirectory = Join-Path $projectRoot "ReleasePackages"

if (-not (Test-Path $cerPath)) {
    throw "Signing certificate not found. Run Build-v1.0-MSIX.ps1 first."
}

$msix = Get-ChildItem $packageDirectory -Recurse -Filter *.msix | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) {
    throw "No MSIX package was found. Run Build-v1.0-MSIX.ps1 first."
}

Write-Host "Trusting the Walker Media Manager local signing certificate for the current user..."
Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null

Write-Host "Installing Walker Media Manager v1.0..."
Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown

Write-Host "Walker Media Manager v1.0 is installed. Open it from the Windows Start menu."
