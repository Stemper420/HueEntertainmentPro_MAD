param(
  [string]$DotNetPath = "dotnet",
  [string]$ProjectPath = "$PSScriptRoot\..\HueArtNet.WinUI\HueArtNet.WinUI.csproj",
  [string]$CertificateThumbprint = "",
  [ValidateSet("CurrentUser", "LocalMachine")]
  [string]$CertificateStore = "CurrentUser",
  [switch]$AllowSelfSigned,
  [switch]$SkipChainValidation
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
  $CertificateThumbprint = [Environment]::GetEnvironmentVariable("HUEARTNET_RELEASE_CERT_THUMBPRINT", "User")
}

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
  $CertificateThumbprint = [Environment]::GetEnvironmentVariable("HUEARTNET_RELEASE_CERT_THUMBPRINT", "Process")
}

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
  throw "Set HUEARTNET_RELEASE_CERT_THUMBPRINT or pass -CertificateThumbprint for release MSIX signing."
}

$normalizedThumbprint = $CertificateThumbprint -replace "\s", ""
$certificate = Get-ChildItem -Path "Cert:\$CertificateStore\My" |
  Where-Object { $_.Thumbprint -eq $normalizedThumbprint } |
  Select-Object -First 1

if ($null -eq $certificate) {
  throw "Release signing certificate '$normalizedThumbprint' was not found in Cert:\$CertificateStore\My."
}

if (-not $certificate.HasPrivateKey) {
  throw "Release signing certificate '$normalizedThumbprint' does not have a private key."
}

$now = Get-Date
if ($certificate.NotAfter -le $now) {
  throw "Release signing certificate '$normalizedThumbprint' expired at $($certificate.NotAfter)."
}

$hasCodeSigningEku = $certificate.EnhancedKeyUsageList |
  Where-Object { $_.ObjectId -eq "1.3.6.1.5.5.7.3.3" -or $_.FriendlyName -eq "Code Signing" }
if (-not $hasCodeSigningEku) {
  throw "Release signing certificate '$normalizedThumbprint' is missing the Code Signing enhanced key usage."
}

if (-not $AllowSelfSigned -and $certificate.Subject -eq $certificate.Issuer) {
  throw "Release signing certificate '$normalizedThumbprint' is self-signed. Use a CA-issued certificate or pass -AllowSelfSigned for internal-only distribution."
}

$projectFullPath = (Resolve-Path $ProjectPath).Path
$projectDirectory = Split-Path $projectFullPath
$manifestPath = Join-Path $projectDirectory "Package.appxmanifest"
$manifest = [xml](Get-Content -LiteralPath $manifestPath)
$publisher = $manifest.Package.Identity.Publisher
if ($certificate.Subject -ne $publisher) {
  throw "Certificate subject '$($certificate.Subject)' does not match package publisher '$publisher'. Update Package.appxmanifest or use a matching release certificate."
}

if (-not $SkipChainValidation) {
  $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
  $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::Online
  $chain.ChainPolicy.RevocationFlag = [System.Security.Cryptography.X509Certificates.X509RevocationFlag]::ExcludeRoot
  $chain.ChainPolicy.VerificationFlags = [System.Security.Cryptography.X509Certificates.X509VerificationFlags]::NoFlag

  if (-not $chain.Build($certificate)) {
    $status = ($chain.ChainStatus | ForEach-Object { $_.StatusInformation.Trim() }) -join "; "
    throw "Release signing certificate '$normalizedThumbprint' did not pass chain validation: $status"
  }
}

& $DotNetPath msbuild $projectFullPath `
  /restore `
  /p:PublishProfile=Msix-x64-ReleaseSigned `
  /p:PackageCertificateThumbprint=$normalizedThumbprint
