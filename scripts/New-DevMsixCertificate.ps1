param(
  [string]$PfxPath = "$PSScriptRoot\..\artifacts\certs\HueArtNet.Dev.pfx",
  [string]$PasswordEnvVar = "HUEARTNET_MSIX_CERT_PASSWORD",
  [switch]$TrustForCurrentUser,
  [switch]$SkipCurrentUserTrust
)

$ErrorActionPreference = "Stop"
$subject = "CN=HueArtNet"
$passwordText = [Environment]::GetEnvironmentVariable($PasswordEnvVar, "User")
if ([string]::IsNullOrWhiteSpace($passwordText)) {
  $passwordText = [Environment]::GetEnvironmentVariable($PasswordEnvVar, "Process")
}

if ([string]::IsNullOrWhiteSpace($passwordText)) {
  throw "Set $PasswordEnvVar as a user or process environment variable before creating the dev MSIX certificate."
}

$password = ConvertTo-SecureString -String $passwordText -AsPlainText -Force
$cert = New-SelfSignedCertificate `
  -Type Custom `
  -Subject $subject `
  -KeyUsage DigitalSignature `
  -KeyExportPolicy Exportable `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @(
    "2.5.29.37={text}1.3.6.1.5.5.7.3.3",
    "2.5.29.19={text}"
  ) `
  -FriendlyName "HueArtNet Dev MSIX"

New-Item -ItemType Directory -Force -Path (Split-Path $PfxPath) | Out-Null
Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $PfxPath -Password $password | Out-Null

if ($TrustForCurrentUser -or -not $SkipCurrentUserTrust) {
  Import-PfxCertificate -FilePath $PfxPath -Password $password -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
}

Write-Host "Created dev MSIX certificate:"
Write-Host "  Subject: $subject"
Write-Host "  Thumbprint: $($cert.Thumbprint)"
Write-Host "  PFX: $((Resolve-Path $PfxPath).Path)"
