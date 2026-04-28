param(
  [Parameter(Mandatory = $true)]
  [string]$PfxPath,
  [securestring]$Password,
  [ValidateSet("CurrentUser", "LocalMachine")]
  [string]$CertificateStore = "CurrentUser",
  [string]$ExpectedSubject = "CN=HueArtNet",
  [ValidateSet("User", "Machine", "Process", "None")]
  [string]$EnvironmentTarget = "User",
  [switch]$AllowSelfSigned,
  [switch]$SkipChainValidation
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PfxPath)) {
  throw "PFX file '$PfxPath' was not found."
}

if ($null -eq $Password) {
  $Password = Read-Host "PFX password" -AsSecureString
}

$resolvedPfxPath = (Resolve-Path -LiteralPath $PfxPath).Path
$storePath = "Cert:\$CertificateStore\My"
$importedCertificates = Import-PfxCertificate `
  -FilePath $resolvedPfxPath `
  -CertStoreLocation $storePath `
  -Password $Password

$now = Get-Date
$certificate = $importedCertificates |
  Where-Object {
    $_.Subject -eq $ExpectedSubject `
      -and $_.HasPrivateKey `
      -and $_.NotAfter -gt $now `
      -and ($_.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq "1.3.6.1.5.5.7.3.3" -or $_.FriendlyName -eq "Code Signing" })
  } |
  Sort-Object NotBefore -Descending |
  Select-Object -First 1

if ($null -eq $certificate) {
  $subjects = ($importedCertificates | ForEach-Object { $_.Subject }) -join "; "
  throw "The PFX was imported, but no valid code-signing certificate with subject '$ExpectedSubject', private key, and future expiry was found. Imported subjects: $subjects"
}

if (-not $AllowSelfSigned -and $certificate.Subject -eq $certificate.Issuer) {
  throw "Imported release signing certificate '$($certificate.Thumbprint)' is self-signed. Use a CA-issued certificate or pass -AllowSelfSigned for internal-only distribution."
}

if (-not $SkipChainValidation) {
  $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
  $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::Online
  $chain.ChainPolicy.RevocationFlag = [System.Security.Cryptography.X509Certificates.X509RevocationFlag]::ExcludeRoot
  $chain.ChainPolicy.VerificationFlags = [System.Security.Cryptography.X509Certificates.X509VerificationFlags]::NoFlag

  if (-not $chain.Build($certificate)) {
    $status = ($chain.ChainStatus | ForEach-Object { $_.StatusInformation.Trim() }) -join "; "
    throw "Imported release signing certificate '$($certificate.Thumbprint)' did not pass chain validation: $status"
  }
}

if ($EnvironmentTarget -ne "None") {
  [Environment]::SetEnvironmentVariable(
    "HUEARTNET_RELEASE_CERT_THUMBPRINT",
    $certificate.Thumbprint,
    $EnvironmentTarget)
}

[pscustomobject]@{
  Store = $storePath
  Subject = $certificate.Subject
  Issuer = $certificate.Issuer
  Thumbprint = $certificate.Thumbprint
  NotAfter = $certificate.NotAfter
  EnvironmentTarget = $EnvironmentTarget
}
