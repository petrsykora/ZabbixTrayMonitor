# Deployment

This page is for administrators who install Zabbix Monitor on several computers. For a single computer, run `…-win-x64-setup.exe` and follow the dialogs.

## Choosing the package

| File | .NET 10 Desktop Runtime | Typical use |
|---|---|---|
| `…-win-x64-setup.exe` | downloaded from Microsoft and installed if missing | single computers, manual installation |
| `…-win-x64-runtime-dependent.msi` | must already be installed, otherwise the installation stops | Group Policy, Intune or scripts where the runtime is deployed separately |
| `…-win-x64.msi` | included | Group Policy, Intune or scripts without a separate runtime, offline computers |
| `…-win-x64-runtime-dependent.zip` / `…-win-x64.zip` | needed / included | portable use, no installation, no autostart |

Both MSI files have the same upgrade code: installing one replaces the other, also with the same version number.

## What the installers do

- install for all users (per machine) to `C:\Program Files\ZabbixTrayMonitor`; administrator rights are required
- add the Start menu shortcut **Zabbix Monitor**
- add autostart for all users: `HKLM\Software\Microsoft\Windows\CurrentVersion\Run`, value `ZabbixTrayMonitor`
- close a running instance before an update or uninstallation
- keep the users' settings and API tokens on uninstall; `setup.exe` also leaves the .NET runtime installed

## Silent installation

MSI (both variants):

```powershell
msiexec /i ZabbixTrayMonitor-v1.3.5-win-x64.msi /qn
```

With a log file:

```powershell
msiexec /i ZabbixTrayMonitor-v1.3.5-win-x64.msi /qn /l*v "$env:TEMP\ZabbixMonitor-install.log"
```

`setup.exe`:

```powershell
ZabbixTrayMonitor-v1.3.5-win-x64-setup.exe /quiet /norestart
```

Silent installations do not start the application; it starts at the next sign-in through the autostart entry.

### MSI properties

| Property | Values | Default | Meaning |
|---|---|---|---|
| `AUTOSTART` | `1`, `0` | `1` | `0` installs without the autostart entry for all users |

Example without autostart:

```powershell
msiexec /i ZabbixTrayMonitor-v1.3.5-win-x64.msi /qn AUTOSTART=0
```

## Update

Install the new version the same way as the first one. The old version is removed automatically (major upgrade) and a running instance is closed first. Downgrades are refused.

### Update notification

Once a day (first one minute after the start) the application asks GitHub for the latest release (`https://api.github.com/repos/petrsykora/ZabbixTrayMonitor/releases/latest`). If it is newer, a notification appears next to the tray icon and the tray menu shows **Update available**. The update window offers **Download** (opens the release page in the browser), **Skip this version** and **Later**. Nothing is downloaded or installed automatically, and no data besides the request itself is sent.

Users can turn the check off under **Settings → Check for updates automatically**; **About → Check for updates** always checks right away.

On managed computers, where new versions are distributed by Group Policy or Intune and users have no administrator rights, turn the automatic check off for all users with this registry value:

| Key | Value | Type | Data |
|---|---|---|---|
| `HKEY_LOCAL_MACHINE\Software\Policies\ZabbixTrayMonitor` | `DisableUpdateCheck` | `REG_DWORD` | `1` |

With Group Policy: **Computer Configuration → Preferences → Windows Settings → Registry → New → Registry Item**, or in a script:

```powershell
New-Item -Path "HKLM:\Software\Policies\ZabbixTrayMonitor" -Force | Out-Null
Set-ItemProperty -Path "HKLM:\Software\Policies\ZabbixTrayMonitor" -Name DisableUpdateCheck -Value 1 -Type DWord
```

The setting in the settings window is then turned off and locked. A manual check from the About window is still possible.

## Uninstall

Interactively: **Settings → Apps → Installed apps → Zabbix Monitor → Uninstall**.

Silently, with the MSI file of the installed version:

```powershell
msiexec /x ZabbixTrayMonitor-v1.3.5-win-x64.msi /qn
```

Or without the file, by the product name:

```powershell
$app = Get-CimInstance Win32_Product -Filter "Name = 'Zabbix Monitor'"
msiexec /x $app.IdentifyingNumber /qn
```

The per-user settings (`%AppData%\ZabbixTrayMonitor`) and the API tokens in the Credential Manager are not removed.

## Group Policy

1. Copy the MSI to a network share that the **computer accounts** can read (for example `Domain Computers` with read access).
2. In Group Policy Management, create or edit a GPO linked to the computers.
3. **Computer Configuration → Policies → Software Settings → Software installation → New → Package**, enter the UNC path of the MSI (not a mapped drive) and choose **Assigned**.
4. The application is installed at the next restart of the computer.

For a new version, add the new MSI to the same GPO and on the **Upgrades** tab set it to upgrade the existing package.

Group Policy software installation cannot pass properties such as `AUTOSTART=0`. If you need them, use a startup script, Intune or another deployment tool.

Use the `…-win-x64.msi` with the included runtime, unless the .NET 10 Desktop Runtime is already deployed on the computers.

## Microsoft Intune

1. **Apps → Windows → Add → Line-of-business app** and upload the MSI. Intune reads the product code and version from the file.
2. Install context: **Device**. Additional command line arguments, if needed: `AUTOSTART=0`.
3. Assign the app to a device group.

For the runtime-dependent MSI, make sure the .NET 10 Desktop Runtime (x64) is deployed first, for example as a dependency in a Win32 app, or use the MSI with the included runtime.

## winget

The package is not in the official winget catalog yet. Installation from the local manifest in this repository is described in the [README](../README.md#winget-local-manifest).

## Settings and API token per user

Every user has their own configuration and token:

| What | Where |
|---|---|
| Settings | `%AppData%\ZabbixTrayMonitor\config.json` |
| API token | Windows Credential Manager of the user, generic credential `ZabbixTrayMonitor.ApiToken` |

When a user starts the application for the first time and there is no `config.json`, the settings window opens.

### Preparing the settings centrally

You can distribute a prepared `config.json`, for example with Group Policy Preferences (**User Configuration → Preferences → Windows Settings → Files**, destination `%AppDataDir%\ZabbixTrayMonitor\config.json`, action **Create** so that users can still change it).

Example with the most important values; all keys are described in the [README](../README.md#config):

```json
{
  "ZabbixUrl": "https://zabbix.example.com",
  "ZabbixDashboardUrl": "https://zabbix.example.com/zabbix.php?action=dashboard.view",
  "PollIntervalSeconds": 60,
  "Language": "",
  "WarningSeverityThreshold": 2,
  "ErrorSeverityThreshold": 4
}
```

Missing keys get their default values. If `config.json` already exists, the settings window does not open automatically: the tray icon turns red with **No API token stored** until the user enters a token under **Settings**.

The API token cannot be part of `config.json`. Each user enters their own token, which is stored encrypted for that user in the Credential Manager. Use a separate Zabbix token per user, so that a token can be revoked without affecting others; see [Zabbix setup](zabbix-setup.md).

## Code signing

Unsigned builds can trigger SmartScreen warnings when downloaded from the internet. How to sign the releases with your own certificate and trust it via Group Policy is described in [Code signing](code-signing.md).
