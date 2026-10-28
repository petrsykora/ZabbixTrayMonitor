# Code signing

To make Windows treat the application as coming from a known publisher, it can be signed with your own code signing certificate. The certificate is distributed to the managed computers via Group Policy (GPO). On computers without this GPO the signature is not trusted and the application behaves like an unsigned one.

The official releases of this repository are currently not signed.

## What is signed

When the secrets below are set, the release workflow signs, with a timestamp so the signature stays valid after the certificate expires:

- `ZabbixTrayMonitor.exe`, `ZabbixTrayMonitor.dll` and the language files `ZabbixTrayMonitor.resources.dll` (both variants)
- both MSI files
- `setup.exe` (the embedded Burn engine first, then the whole bundle)

The .NET runtime files are already signed by Microsoft and stay as they are. Without the secrets the build stays unsigned.

## 1. Create the certificate (once)

On a Windows computer in PowerShell:

```powershell
.\tools\New-CodeSigningCertificate.ps1 -Subject "CN=My Company Code Signing" -OutputFolder C:\Signing
```

Optional parameters: `-ValidYears` (default 5) and `-FileName` (default `ZabbixTrayMonitor-CodeSigning`).

The script asks for a password and creates:

- `ZabbixTrayMonitor-CodeSigning.pfx`: private key, keep it safe and never commit it to the repository
- `ZabbixTrayMonitor-CodeSigning.cer`: public certificate for the GPO
- `ZabbixTrayMonitor-CodeSigning.pfx.base64.txt`: value for the GitHub secret, delete it afterwards

The certificate is removed from your certificate store afterwards, so the private key only exists in the PFX file. Keep the PFX file and its password, for example in a password manager: GitHub secrets cannot be read back. If you lose them, create a new certificate and distribute the new `.cer` file again.

To recreate the secret value from an existing PFX file:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\Signing\ZabbixTrayMonitor-CodeSigning.pfx")) | Set-Clipboard
```

## 2. Set the GitHub secrets

In the repository under **Settings → Secrets and variables → Actions → New repository secret**:

- `SIGNING_CERT_PFX_BASE64`: content of the `.pfx.base64.txt` file
- `SIGNING_CERT_PASSWORD`: the password from step 1

The next run of the release workflow signs the files.

## 3. Distribute the certificate via GPO

In Group Policy Management, create a GPO for the affected computers and import the `.cer` file in **two** places:

- **Computer Configuration → Policies → Windows Settings → Security Settings → Public Key Policies → Trusted Root Certification Authorities**
- **… → Public Key Policies → Trusted Publishers**

Without a GPO (for example for testing), on a single computer as administrator:

```powershell
Import-Certificate -FilePath .\ZabbixTrayMonitor-CodeSigning.cer -CertStoreLocation Cert:\LocalMachine\Root
Import-Certificate -FilePath .\ZabbixTrayMonitor-CodeSigning.cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher
```

## 4. Check the signature

```powershell
Get-AuthenticodeSignature "C:\Program Files\ZabbixTrayMonitor\ZabbixTrayMonitor.exe"
```

`Status` must be `Valid`. `UnknownError` or `NotTrusted` means the certificate is not trusted on this computer (step 3 missing).

## Renewing the certificate

Before the certificate expires, create a new one (step 1), distribute the new `.cer` file (step 3) and then replace the secrets (step 2). Files signed with the old certificate stay valid thanks to the timestamp.

## Note on SmartScreen

SmartScreen mainly checks files downloaded from the internet with a browser (Mark of the Web). Your own certificate has no reputation with Microsoft, so a file downloaded directly from GitHub can still trigger a warning. To distribute without a warning, roll the application out internally (GPO software deployment, Intune, a network share in the intranet zone), or unblock a downloaded file before using it:

```powershell
Unblock-File .\ZabbixTrayMonitor-v1.3.3-win-x64.zip
```
