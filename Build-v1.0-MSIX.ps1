[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$CertificatePassword
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot "WalkerMediaManager.UI\WalkerMediaManager.UI.csproj"
$certificateDirectory = Join-Path $projectRoot "ReleaseCertificate"
$pfxPath = Join-Path $certificateDirectory "WalkerMediaManager-v1.0.pfx"
$cerPath = Join-Path $certificateDirectory "WalkerMediaManager-v1.0.cer"
$subject = "CN=Walker Software"

if (-not [Environment]::Is64BitOperatingSystem) {
    throw "Walker Media Manager v1.0 requires 64-bit Windows 11."
}

$windowsVersion = [Environment]::OSVersion.Version
if ($windowsVersion.Build -lt 22000) {
    throw "Windows 11 build 22000 or newer is required."
}

if ([string]::IsNullOrWhiteSpace($CertificatePassword)) {
    $securePassword = Read-Host "Create a password for the local signing certificate" -AsSecureString
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    try {
        $CertificatePassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}
else {
    $securePassword = ConvertTo-SecureString $CertificatePassword -AsPlainText -Force
}

New-Item -ItemType Directory -Force -Path $certificateDirectory | Out-Null

if (-not (Test-Path $pfxPath)) {
    Write-Host "Creating a local code-signing certificate..."
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -KeyUsage DigitalSignature `
        -FriendlyName "Walker Media Manager v1.0 Local Signing" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3") `
        -NotAfter (Get-Date).AddYears(5)

    Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $securePassword | Out-Null
    Export-Certificate -Cert $certificate -FilePath $cerPath | Out-Null
}
elseif (-not (Test-Path $cerPath)) {
    throw "The PFX exists but the CER file is missing. Delete the ReleaseCertificate folder and run this script again."
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "Visual Studio 2022 was not found. Install Visual Studio 2022 with .NET desktop development and Windows application development workloads."
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($msbuild) -or -not (Test-Path $msbuild)) {
    throw "MSBuild could not be located."
}

$packageDirectory = Join-Path $projectRoot "ReleasePackages"
if (Test-Path $packageDirectory) {
    Remove-Item $packageDirectory -Recurse -Force
}

Write-Host "Restoring and building Walker Media Manager v1.0..."
& $msbuild $projectFile `
    /restore `
    /m `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /p:GenerateAppxPackageOnBuild=true `
    /p:AppxPackageSigningEnabled=true `
    /p:PackageCertificateKeyFile="$pfxPath" `
    /p:PackageCertificatePassword="$CertificatePassword" `
    /p:AppxBundle=Never `
    /p:UapAppxPackageBuildMode=SideloadOnly

if ($LASTEXITCODE -ne 0) {
    throw "The v1.0 package build failed with exit code $LASTEXITCODE."
}

$msix = Get-ChildItem $packageDirectory -Recurse -Filter *.msix | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) {
    throw "The build completed, but no MSIX package was found in $packageDirectory."
}

Write-Host ""
Write-Host "Walker Media Manager v1.0 package created successfully."
Write-Host "MSIX: $($msix.FullName)"
Write-Host "Certificate: $cerPath"
Write-Host "Run Install-v1.0.ps1 to install it on this computer."

