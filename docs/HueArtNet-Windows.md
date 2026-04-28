# Windows Art-Net Controller

`HueArtNet.WinUI` is a local Windows controller for external DMX/Art-Net software. It receives Art-Net `ArtDMX` packets and streams the mapped RGB output to Philips Hue Entertainment areas.

## Scope

- Receives Art-Net over UDP, default port `6454`.
- Controls up to 4 Hue bridge / Entertainment group mappings.
- Stores Hue credentials locally in SQLite, protected with Windows DPAPI.
- Supports fixture modes:
  - `Rgb3`: red, green, blue
  - `Rgbww5`: red, green, blue, cool white, warm white
  - `DimmerRgbww6`: dimmer, red, green, blue, cool white, warm white
- Builds as a WinUI app and can be packaged as unsigned or dev-signed MSIX.

## Build

Use a .NET SDK that supports the solution target frameworks.

```powershell
dotnet build .\HueEntertainmentPro.sln
dotnet test .\HueEntertainmentPro.sln
```

Run the app from Visual Studio or build the WinUI project directly:

```powershell
dotnet build .\HueArtNet.WinUI\HueArtNet.WinUI.csproj
```

## Hue Pairing

1. Open `HueArtNet.WinUI`.
2. Click `Discover bridges`.
3. Select a discovered Hue bridge.
4. Select the hub row to update.
5. Press the physical Hue bridge link button.
6. Click `Pair selected`.
7. Select the Hue Entertainment group for that bridge.
8. Save the profile.

Pairing writes the bridge IP, Hue application key, Hue entertainment key, Entertainment group ID, and stable Hue bridge ID into the selected hub mapping.

## Art-Net Mapping

Each enabled hub mapping listens to one Art-Net universe.

- `Universe`: Art-Net universe number.
- `Start channel`: first DMX channel, using UI numbering `1..512`.
- `Fixture mode`: per-light DMX layout.
- `Light order`: Hue entertainment channel IDs in fixture order.

Example for `Rgb3`, `Start channel = 1`, `Light order = 3,2,1`:

- Light/channel `3`: DMX `1,2,3`
- Light/channel `2`: DMX `4,5,6`
- Light/channel `1`: DMX `7,8,9`

## Running With Art-Net Software

1. Start the profile in `HueArtNet.WinUI`.
2. In the external lighting software, send Art-Net `ArtDMX` to the selected local IP and UDP port `6454`.
3. Configure the same universes and channel offsets as the WinUI profile.
4. Allow UDP traffic through Windows Firewall if packets do not arrive.

The runtime ignores unconfigured universes.

## Fail-Safe Modes

The profile timeout mode controls what happens after incoming Art-Net stops:

- `Hold last frame`
- `Blackout`
- `Restore startup frame`

## Unsigned MSIX

Unsigned sideload package:

```powershell
dotnet msbuild .\HueArtNet.WinUI\HueArtNet.WinUI.csproj /restore /p:PublishProfile=Msix-x64
```

Output:

```text
artifacts\msix\x64\HueArtNet.WinUI_1.0.0.0_x64_Test\HueArtNet.WinUI_1.0.0.0_x64.msix
```

## Portable ZIP

Use this helper when you want a portable Windows folder plus a zip archive:

```powershell
.\scripts\Publish-PortableZip.ps1
```

The script publishes `HueArtNet.WinUI` as:

- self-contained
- `win-x64`
- single-file
- `WindowsPackageType=None`
- `WindowsAppSDKSelfContained=true`

Output:

```text
artifacts\portable\win-x64\HueArtNet.WinUI.exe
artifacts\HueArtNet.WinUI_portable_win-x64.zip
```

The published folder includes `README_RUN.txt` with launch notes, and the script prints the ZIP SHA256 after packaging.
The portable folder contains a `HueArtNet.portable` marker so the app stores its
database and logs in the local `data` folder next to `HueArtNet.WinUI.exe`.
Do not rename `HueArtNet.WinUI.exe`; WinUI resource loading expects that file
name. Hue pairing secrets are DPAPI-protected for the current Windows user, so
pair bridges again after moving the archive to another machine or Windows user.

## Dev-Signed MSIX

Create a local dev certificate. The password is read from `HUEARTNET_MSIX_CERT_PASSWORD` and is not committed.

```powershell
$env:HUEARTNET_MSIX_CERT_PASSWORD = "<local-dev-password>"
[Environment]::SetEnvironmentVariable("HUEARTNET_MSIX_CERT_PASSWORD", $env:HUEARTNET_MSIX_CERT_PASSWORD, "User")
.\scripts\New-DevMsixCertificate.ps1
```

Publish the signed package:

```powershell
.\scripts\Publish-DevMsix.ps1
```

Output:

```text
artifacts\msix\x64-signed\HueArtNet.WinUI_1.0.0.0_x64_Test\HueArtNet.WinUI_1.0.0.0_x64.msix
```

Notes:

- The certificate subject must match the package publisher `CN=HueArtNet`.
- The dev script trusts the generated cert in `Cert:\CurrentUser\TrustedPeople` for local sideloading.
- A self-signed cert is for development only. Release distribution should use a stable release certificate or a trusted signing service.
- `Get-AuthenticodeSignature` can still report an untrusted root for a self-signed cert unless the root is trusted. The publish script verifies that the selected signing certificate is trusted in `CurrentUser\TrustedPeople`.

## Release-Signed MSIX

Release packages must be signed with a stable code-signing certificate whose subject exactly matches `Package.appxmanifest`:

```xml
Publisher="CN=HueArtNet"
```

Import a CA-issued PFX and persist its thumbprint for release signing:

```powershell
.\scripts\Import-ReleaseMsixCertificate.ps1 -PfxPath C:\certs\HueArtNet.Release.pfx
```

The import script prompts for the PFX password as a secure string and sets
`HUEARTNET_RELEASE_CERT_THUMBPRINT` for the current user by default.

If the certificate is already installed, set the release certificate thumbprint
as a user environment variable:

```powershell
[Environment]::SetEnvironmentVariable("HUEARTNET_RELEASE_CERT_THUMBPRINT", "<thumbprint>", "User")
```

Publish with:

```powershell
.\scripts\Publish-ReleaseMsix.ps1
```

Output:

```text
artifacts\msix\x64-release\HueArtNet.WinUI_1.0.0.0_x64_Test\HueArtNet.WinUI_1.0.0.0_x64.msix
```

The release script validates:

- the certificate exists in `Cert:\CurrentUser\My` by default;
- the certificate has a private key;
- the certificate is not expired;
- the certificate has Code Signing enhanced key usage;
- the certificate subject matches the package publisher;
- the certificate is not self-signed unless `-AllowSelfSigned` is explicitly passed;
- the certificate chain validates unless `-SkipChainValidation` is explicitly passed.

To use a machine-level certificate:

```powershell
.\scripts\Import-ReleaseMsixCertificate.ps1 -PfxPath C:\certs\HueArtNet.Release.pfx -CertificateStore LocalMachine -EnvironmentTarget Machine
.\scripts\Publish-ReleaseMsix.ps1 -CertificateStore LocalMachine
```

## Verification Checklist

Before shipping a build:

```powershell
dotnet test .\HueEntertainmentPro.sln -v minimal
dotnet build .\HueEntertainmentPro.sln -v minimal
dotnet msbuild .\HueArtNet.WinUI\HueArtNet.WinUI.csproj /restore /p:PublishProfile=Msix-x64
.\scripts\Publish-DevMsix.ps1
.\scripts\Publish-ReleaseMsix.ps1
```

Manual verification still requires real Hue hardware and external Art-Net software.
