param(
  [string]$DotNetPath = "dotnet",
  [string]$ProjectPath = "$PSScriptRoot\..\HueArtNet.WinUI\HueArtNet.WinUI.csproj",
  [string]$CertificateSubject = "CN=HueArtNet",
  [string]$CertificateThumbprint = "",
  [switch]$AllowUntrustedCertificate
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
  $now = Get-Date
  $certificate = Get-ChildItem -Path "Cert:\CurrentUser\My" |
    Where-Object {
      $_.Subject -eq $CertificateSubject `
        -and $_.HasPrivateKey `
        -and $_.NotAfter -gt $now `
        -and ($_.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq "1.3.6.1.5.5.7.3.3" -or $_.FriendlyName -eq "Code Signing" })
    } |
    Sort-Object NotBefore -Descending |
    Select-Object -First 1

  if ($null -eq $certificate) {
    throw "No signing certificate with subject '$CertificateSubject' and a private key was found in Cert:\CurrentUser\My. Run scripts\New-DevMsixCertificate.ps1 first."
  }

  $CertificateThumbprint = $certificate.Thumbprint
}

if (-not $AllowUntrustedCertificate) {
  $trustedCertificate = Get-ChildItem -Path "Cert:\CurrentUser\TrustedPeople" |
    Where-Object { $_.Thumbprint -eq $CertificateThumbprint } |
    Select-Object -First 1

  if ($null -eq $trustedCertificate) {
    throw "Signing certificate '$CertificateThumbprint' is not trusted in Cert:\CurrentUser\TrustedPeople. Run scripts\New-DevMsixCertificate.ps1 or pass -AllowUntrustedCertificate for signing-only builds."
  }
}

& $DotNetPath msbuild $ProjectPath `
  /restore `
  /p:PublishProfile=Msix-x64-Signed `
  /p:PackageCertificateThumbprint=$CertificateThumbprint
