param(
  [string]$DotNetPath = "dotnet",
  [string]$ProjectPath = "$PSScriptRoot\..\HueArtNet.WinUI\HueArtNet.WinUI.csproj",
  [string]$Configuration = "Release",
  [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$projectFullPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$repoRoot = Split-Path $PSScriptRoot -Parent
$artifactsRoot = Join-Path $repoRoot "artifacts"
$portableRoot = Join-Path $artifactsRoot "portable"
$outputDir = Join-Path $portableRoot "win-x64"
$zipPath = Join-Path $artifactsRoot "HueArtNet.WinUI_portable_win-x64.zip"

if (Test-Path -LiteralPath $outputDir) {
  Remove-Item -LiteralPath $outputDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
  Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$publishArgs = @(
  "publish",
  $projectFullPath,
  "-c", $Configuration,
  "-r", $RuntimeIdentifier,
  "-o", $outputDir,
  "/p:WindowsPackageType=None",
  "/p:WindowsAppSDKSelfContained=true",
  "/p:SelfContained=true",
  "/p:PublishSingleFile=true",
  "/p:PublishTrimmed=false",
  "/p:PublishReadyToRun=false",
  "/p:EnableCompressionInSingleFile=true"
)

& $DotNetPath @publishArgs
if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exePath = Join-Path $outputDir "HueArtNet.WinUI.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
  throw "Expected portable executable '$exePath' was not created."
}

Get-ChildItem -LiteralPath $outputDir -Filter *.pdb -File -ErrorAction SilentlyContinue |
  Remove-Item -Force

$dataDir = Join-Path $outputDir "data"
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
Set-Content -LiteralPath (Join-Path $outputDir "HueArtNet.portable") -Value "portable" -Encoding ASCII

$readmePath = Join-Path $outputDir "README_RUN.txt"
$readme = @"
HueArtNet.WinUI portable build

Run HueArtNet.WinUI.exe from this folder. Do not rename the executable; WinUI resource loading expects this file name.
Keep the extracted folder together so the app can find its files.
This build is self-contained and does not require a separate .NET runtime installation.

The app stores portable profile data and logs in the local data folder next to the executable.
Hue pairing secrets are protected by Windows DPAPI for the current Windows user, so when moving this folder to another machine or user account, pair the Hue bridges again.
"@

Set-Content -LiteralPath $readmePath -Value $readme -Encoding UTF8

Compress-Archive -Path (Join-Path $outputDir "*") -DestinationPath $zipPath -Force

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash
Write-Host "Portable ZIP: $zipPath"
Write-Host "SHA256: $hash"
