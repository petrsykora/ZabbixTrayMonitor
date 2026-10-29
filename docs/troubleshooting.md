# Troubleshooting

The tray icon shows the state at a glance: green no problems, yellow warnings, red errors **or no connection**, gray not loaded yet. When there is no connection, the tooltip shows the error message.

## Where to look

| What | Where |
|---|---|
| Settings | `%AppData%\ZabbixTrayMonitor\config.json` (the **About** window has a link to the folder) |
| API token | **Credential Manager → Windows Credentials → Generic Credentials**, entry `ZabbixTrayMonitor.ApiToken` |
| Installation log | only if you install with `/l*v`, see [Deployment](deployment.md#silent-installation) |

## Connection

### "No Zabbix URL configured" / "No API token stored"

The settings are incomplete. Open **Settings** from the tray menu, enter the URL and the token and click **Save**.

### What the Test button checks

**Test** in the settings first checks that the Zabbix API can be reached, then that the token works:

- **Connection successful** with the Zabbix version and the number of visible problems: URL and token are fine.
- **The Zabbix server can be reached, but the API token does not work**: the URL is right, the error message says what is wrong with the token (see below).
- **The API token was not checked**: no token is entered and none is stored.
- **Connection failed**: the server cannot be reached; see 404, timeout and certificate errors below.

The test uses the values in the window, also before they are saved.

### "Not authorized" or "Session terminated"

The token is wrong, has expired, was disabled or deleted in Zabbix, or its user was disabled. Create a new token and enter it under **Settings → API token**.

### "No permissions to call ..."

The user role does not allow this API method. Add it to the allow list of the role, see [Zabbix setup](zabbix-setup.md#1-user-role).

### 404 Not Found

The URL points to the wrong place. Enter the address of the Zabbix web interface, without `/api_jsonrpc.php` and without `/zabbix.php`. If Zabbix runs in a subdirectory, include it: `https://example.com/zabbix`.

### Timeout or "No such host is known"

- Open the Zabbix URL in a browser on the same computer.
- The application waits at most 15 seconds per request. A Zabbix server that answers slower is treated as unreachable.
- The application uses the Windows proxy settings. If the proxy requires authentication (HTTP 407), add the Zabbix server to the proxy exceptions.

### Certificate or SSL errors

Windows does not trust the certificate of the Zabbix server. Typical causes:

- the certificate is self-signed or issued by an internal certification authority that is not trusted on this computer
- the host name in the URL does not match the certificate (for example an IP address instead of the DNS name)
- the certificate has expired

Fix: import the server certificate or the internal CA certificate into **Trusted Root Certification Authorities** of the computer, for many computers via Group Policy. Check it by opening the Zabbix URL in Edge: there must be no certificate warning.

**Ignore SSL certificate errors** in the settings makes the error go away, but anybody on the network could then pretend to be the Zabbix server and steal the token. Use it for testing only.

### Warning about http:// when saving

The token would be sent unencrypted. Switch the Zabbix web server to HTTPS. You can still save, but it is not recommended.

## Problems

### A problem from Zabbix is missing

- **Severity:** problems below the warning threshold are not loaded (default: Not classified and Information are ignored). See **Settings → Warning threshold**.
- **Suppressed:** suppressed problems and problems of hosts in maintenance are hidden unless **Keep showing suppressed problems** is enabled.
- **Permissions:** the user of the token only sees problems of host groups it has read access to.
- **Disabled objects:** problems of disabled triggers, hosts or items are not shown, just like in the Zabbix frontend.
- **Symptoms:** symptom problems (cause and symptom events) are not shown as separate rows.
- **Not refreshed yet:** the list is refreshed every poll interval (default 60 seconds); **Refresh** in the tray menu loads it immediately.

### Acknowledging or suppressing fails

- The role of the user needs the actions **Acknowledge problems**, **Add problem comments** and, for suppressing, **Suppress problems**.
- Suppressing needs Zabbix 6.2 or newer.
- The time for "Suppress until" must be in the future.

### The problem is still shown after an action

Zabbix processes the action asynchronously. With **Refresh automatically after acknowledging/suppressing** enabled, the application refreshes right after the action and once more about 4 seconds later. Otherwise use **Refresh**.

## Settings

### Reset all settings

1. Exit the application (tray menu → **Exit**).
2. Delete or rename `%AppData%\ZabbixTrayMonitor\config.json`.
3. Optionally delete the token: **Credential Manager → Windows Credentials → `ZabbixTrayMonitor.ApiToken` → Remove**.
4. Start the application; the settings window opens.

### config.corrupt.json

If `config.json` cannot be read (for example after manual editing with a syntax error), the application uses the default settings and keeps a copy of the unreadable file as `config.corrupt.json` in the same folder. Fix or delete `config.json`; the copy can be deleted afterwards.

### The language does not change

With **Language: System**, the application follows the Windows display language; if it is not English, German or Czech, English is used. Choose a language explicitly under **Settings → Language**.

## Installation

### "Zabbix Monitor requires the .NET 10 Desktop Runtime (x64)"

The runtime-dependent MSI was started on a computer without the runtime. Use `…-setup.exe` (installs the runtime if needed), the `…-win-x64.msi` with the included runtime, or install the runtime from <https://dotnet.microsoft.com/download/dotnet/10.0> first.

### "A newer version of Zabbix Monitor is already installed"

Downgrades are not possible. Uninstall the newer version first if you really need the older one.

### Windows SmartScreen: "Windows protected your PC"

The release files are not signed with a certificate trusted by Microsoft. Click **More info → Run anyway**, or unblock the file before running it:

```powershell
Unblock-File .\ZabbixTrayMonitor-v1.3.7-win-x64-setup.exe
```

For managed computers, sign the files with your own certificate and trust it via Group Policy, see [Code signing](code-signing.md), or distribute the MSI internally (Group Policy, Intune), which does not trigger SmartScreen.

### The application does not start after sign-in

- The autostart entry is missing if the MSI was installed with `AUTOSTART=0`.
- Check **Task Manager → Startup apps**: `ZabbixTrayMonitor` must be enabled.
- The portable ZIP versions have no autostart.

### The tray icon is not visible

Windows 11 hides new tray icons in the overflow area. Click **^** next to the clock, or enable the icon under **Settings → Personalization → Taskbar → Other system tray icons**.
