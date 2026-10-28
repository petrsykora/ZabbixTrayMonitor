# Zabbix Monitor

Small Windows tray client for Zabbix.

The application runs in the Windows system tray and shows the current Zabbix status through a colored tray icon, a tooltip and a small problems window.

The tray icon shows at a glance whether everything is fine, there are warnings or there are errors. The tooltip and the problems window list the current relevant Zabbix problems.

## Features

- Tray status for OK, warning, error and unknown state
- Automatic refresh with a configurable poll interval
- Compact tooltip with the most important current problems
- Configurable delay before the tray tooltip opens
- Problems window with host, time, message and monitored object
- Dark and light mode
- User interface in English, German and Czech; follows the Windows display language by default (English if the system language is not supported) or can be fixed in the settings
- API token stored in the Windows Credential Manager
- Alarm handling directly in the problems window:
  - Acknowledge only
  - Suppress for 5 minutes
  - Suppress for 15 minutes
  - Suppress for 1 hour
  - Suppress for 3 hours
  - Suppress for 1 day
  - Suppress until a freely chosen date/time
- Configurable default message for acknowledgements/suppressions
- The message can be overridden per action directly in the context menu
- Suppressed problems can optionally still be shown
- Optional automatic refresh after an alarm action, including a delayed follow-up refresh

## Download

[![Download](https://img.shields.io/badge/Download-Releases-blue)](https://github.com/petrsykora/ZabbixTrayMonitor/releases)

## Installation

### MSI installer (recommended)

Download `ZabbixTrayMonitor-<version>-win-x64.msi` from the releases and run it. The installer:

- installs the application for all users to `C:\Program Files\ZabbixTrayMonitor` (administrator rights required)
- adds a Start menu shortcut and starts the application automatically when a user signs in
- replaces older versions automatically on update and closes a running instance first
- keeps the user's settings (`config.json`) and the API token in the Credential Manager on uninstall

Silent installation, for example for scripts or Intune:

```powershell
msiexec /i ZabbixTrayMonitor-v1.3.1-win-x64.msi /qn
```

Without autostart:

```powershell
msiexec /i ZabbixTrayMonitor-v1.3.1-win-x64.msi /qn AUTOSTART=0
```

Deployment via Group Policy: put the MSI on a network share that the computer accounts can read and assign it under **Computer Configuration → Policies → Software Settings → Software installation → New → Package** (choose **Assigned**). It is installed on the next restart. For a new version, add the new MSI to the same GPO and set it to upgrade the existing package.

### ZIP (portable)

1. Download and extract the latest release ZIP.
2. Start `ZabbixTrayMonitor.exe`.

### First start

1. On first start the **Settings** window opens.
2. Enter the **Zabbix URL** and the **API token**.
3. Check the connection with **Test**, then click **Save**.

## Screenshots

### Tray tooltip without problems

<img src="Assets/README/tray-tooltip-ok.png" alt="Tray tooltip without problems">

### Tray tooltip with problems

<img src="Assets/README/tray-tooltip-problem.png" alt="Tray tooltip with problems">

### Problems window

<img src="Assets/README/problems-window.png" alt="Problems window" width="290">

### Alarm handling

<img src="Assets/README/alarm-editing.png" alt="Alarm handling" width="420">

### Settings

<img src="Assets/README/settings-window.png" alt="Settings" width="420">

## Config

The configuration is stored in:

```text
%AppData%\ZabbixTrayMonitor\config.json
```

Example:

```json
{
  "ZabbixUrl": "https://zabbix.home",
  "ZabbixDashboardUrl": "https://zabbix.home/zabbix.php?action=dashboard.view&dashboardid=407",
  "PollIntervalSeconds": 60,
  "TrayToolTipDelayMilliseconds": 250,
  "IgnoreCertificateErrors": false,
  "UseDarkMode": true,
  "Language": "",
  "ShowSuppressedProblems": false,
  "RefreshAfterProblemAction": true,
  "AcknowledgeMessage": "Acknowledged by john.doe",
  "WarningSeverityThreshold": 2,
  "ErrorSeverityThreshold": 4,
  "CredentialTargetSuffix": "ApiToken",
  "CredentialUsername": "api-token"
}
```

## Languages

The user interface is available in English, German and Czech. By default it follows the Windows display language; if that language is not supported, English is used. Under **Settings → Language** you can choose a fixed language instead (`Language` in config.json: `""` for the system language, `"en"`, `"de"` or `"cs"`). The change takes effect right after saving.

The texts are stored in `Resources/Strings.resx` (English), `Resources/Strings.de.resx` and `Resources/Strings.cs.resx`. To add another language, add a `Strings.<code>.resx` file and add the code to `LocalizationService.SupportedLanguages` and to the language selection in the settings window.

## Severity mapping

The Zabbix API returns severity values between 0 and 5:

```text
0 = Not classified
1 = Information
2 = Warning
3 = Average
4 = High
5 = Disaster
```

`WarningSeverityThreshold` defines from which severity a problem counts as a warning.

`ErrorSeverityThreshold` defines from which severity a problem counts as an error.

Defaults:

```text
Severity 0-1 -> ignored
Severity 2-3 -> warning
Severity 4-5 -> error
```

## Alarm handling

Right-clicking a problem opens the `Edit alarm` menu.

`Acknowledge only...` runs `event.acknowledge` in Zabbix with an acknowledgement and a message.

The time-limited suppressions acknowledge the event, add the message and suppress the problem until the selected time.

If `ShowSuppressedProblems` is `false`, suppressed problems are hidden from the tray status, the tooltip and the problems window.

## API

The application uses the Zabbix JSON-RPC API.

The API path is fixed to `/api_jsonrpc.php` as defined by Zabbix. If Zabbix runs in a subdirectory, include it in the Zabbix URL (for example `https://zabbix.example.com/zabbix`). Older configurations with a custom API path are migrated automatically.

Authentication uses a bearer token.

For the problem list, the current problems are queried in bulk and enriched with host names and trigger/item information. No separate API request is made per problem.

Zabbix API methods used:

```text
problem.get
event.get
trigger.get
event.acknowledge
apiinfo.version
```

The user behind the API token needs the required Zabbix permissions. For alarm handling, acknowledging and suppressing the affected events must be allowed.

### Security notes

- The API token is sent with every request. Connect to the Zabbix server via **https://**; for http:// the application asks for confirmation when saving.
- `IgnoreCertificateErrors` disables certificate validation completely. An attacker on the network could then impersonate the Zabbix server and steal the token. For self-signed certificates, import the certificate or your own CA as trusted in Windows instead.
- Use a dedicated Zabbix user with as few permissions as possible for the token (read access to the relevant host groups, and acknowledge/suppress if needed).

## Code signing

To make Windows treat the application as coming from a known publisher, it can be signed with your own code signing certificate. The certificate is distributed to the managed computers via Group Policy (GPO). On computers without this GPO the signature is not trusted and the application behaves like an unsigned one.

### 1. Create the certificate (once)

On a Windows computer in PowerShell:

```powershell
.\tools\New-CodeSigningCertificate.ps1 -Subject "CN=My Company Code Signing" -OutputFolder C:\Signing
```

The script asks for a password and creates:

- `ZabbixTrayMonitor-CodeSigning.pfx`: private key, keep it safe and never commit it to the repository
- `ZabbixTrayMonitor-CodeSigning.cer`: public certificate for the GPO
- `ZabbixTrayMonitor-CodeSigning.pfx.base64.txt`: value for the GitHub secret, delete it afterwards

### 2. Set the GitHub secrets

In the repository under **Settings → Secrets and variables → Actions → New repository secret**:

- `SIGNING_CERT_PFX_BASE64`: content of the `.pfx.base64.txt` file
- `SIGNING_CERT_PASSWORD`: the password from step 1

The release workflow then signs `ZabbixTrayMonitor.exe`, `ZabbixTrayMonitor.dll`, the language files and the MSI installer automatically, with a timestamp so the signature stays valid after the certificate expires. Without the secrets the build stays unsigned as before.

### 3. Distribute the certificate via GPO

In Group Policy Management, create a GPO for the affected computers and import the `.cer` file in **two** places:

- **Computer Configuration → Policies → Windows Settings → Security Settings → Public Key Policies → Trusted Root Certification Authorities**
- **… → Public Key Policies → Trusted Publishers**

Without a GPO (for example for testing), on a single computer as administrator:

```powershell
Import-Certificate -FilePath .\ZabbixTrayMonitor-CodeSigning.cer -CertStoreLocation Cert:\LocalMachine\Root
Import-Certificate -FilePath .\ZabbixTrayMonitor-CodeSigning.cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher
```

Check: `Get-AuthenticodeSignature .\ZabbixTrayMonitor.exe` must show `Status: Valid`.

### Note on SmartScreen

SmartScreen mainly checks files downloaded from the internet with a browser (Mark of the Web). Your own certificate has no reputation with Microsoft, so a ZIP downloaded directly from GitHub can still trigger a warning. To distribute without a warning, roll the application out internally (GPO software deployment, Intune, a network share in the intranet zone), or unblock a downloaded ZIP before extracting it:

```powershell
Unblock-File .\ZabbixTrayMonitor-v1.3.1-win-x64.zip
```

## Libraries

### Hardcodet.NotifyIcon.Wpf

WPF support for Windows system tray icons.

### CredentialManagement

Access to the Windows Credential Manager.
