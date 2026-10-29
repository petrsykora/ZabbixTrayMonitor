# Changelog

All notable changes to this project. Versions up to 1.2.1 were released by Darano94 in the [original project](https://github.com/Darano94/ZabbixTrayMonitor).

## Unreleased

### Added

- Updates can be installed from the application: **Install** in the update window, or **Settings → Install updates automatically** (off by default). The installer of the installed variant is downloaded from the GitHub release, its SHA-256 checksum and its signature with the project's certificate are verified, and it is installed after the administrator confirmation of Windows. The application is closed and started again; the autostart setting is kept. The portable ZIP versions are only notified.

## 1.3.9 – 2026-10-29

### Added

- The About window shows the time and the result of the last update check, including errors of the automatic check, which are otherwise not shown.

### Fixed

- The Start menu shortcut created by the installers showed a blank (white) icon; it now uses the icon of the application.
- README and code signing docs: the releases are signed since version 1.3.7 (the version bump had changed it to 1.3.8).

## 1.3.8 – 2026-10-29

### Changed

- The automatic update check runs every 6 hours instead of once a day, so a new version is noticed sooner without restarting the application.

## 1.3.7 – 2026-10-29

### Changed

- The release files are signed with a code signing certificate of the project (trusted on computers that receive the certificate via Group Policy).
- README: the code signing section describes how to check the release files.

## 1.3.6 – 2026-10-29

### Added

- **VirusTotal** workflow to check the files of an existing release again and update its result.
- Code signing policy and privacy policy in the README; the built-in help lists the network connections of the application.

## 1.3.5 – 2026-10-29

### Added

- Notification when a new version is available: once a day the application checks the latest release on GitHub. The update window offers **Download** (release page), **Skip this version** and **Later**; nothing is installed automatically.
- **About → Check for updates** and the setting **Check for updates automatically**. Administrators can turn the check off with the registry policy `HKLM\Software\Policies\ZabbixTrayMonitor`, `DisableUpdateCheck = 1`, see [Deployment](docs/deployment.md#update-notification).
- Optional VirusTotal check of the release files in the release workflow: the result is added to the release notes and attached as `virustotal-badge.json` for a README badge, see [Development](docs/development.md#virustotal).

## 1.3.4 – 2026-10-29

### Fixed

- The application icon was blank (white) in the Alt+Tab switcher: the small icon sizes are now stored as bitmaps instead of PNG.

## 1.3.3 – 2026-10-28

### Changed

- **Test** in the settings also checks the API token (before, it only checked that the server can be reached) and shows the number of problems visible to the token.
- The project can be restored and built on Linux, which fixes GitHub's automatic dependency submission.
- Documentation for administrators and developers in `docs/`, and this changelog.

## 1.3.2 – 2026-10-28

### Added

- `…-setup.exe`: installs the .NET 10 Desktop Runtime only if it is missing, then the application.
- Small runtime-dependent MSI and ZIP without the bundled .NET runtime. The MSI refuses to install without the runtime and says where to get it.
- License terms (MIT) in the MSI and `setup.exe` dialogs.
- Option to start the application at the end of an interactive installation.
- Application and installer icon.
- winget manifests for installation from a local copy of the repository.

### Changed

- Fonts follow the Windows text size setting (**Accessibility → Text size**).
- README with badges and a description of all installation variants.

## 1.3.1 – 2026-10-28

### Added

- MSI installer for all users with Start menu shortcut and autostart (`AUTOSTART=0` to turn it off), suitable for Group Policy and Intune.
- Built-in help (tray menu → **Help** or F1) with the complete documentation.
- About window with version, origin, copyright and license.
- The MIT license is shipped as `LICENSE.txt` with every build.
- `SECURITY.md`, Dependabot and CodeQL.

### Changed

- Settings simplified: the application name is fixed to "Zabbix Monitor", the API path is fixed to `/api_jsonrpc.php` (a subdirectory belongs in the Zabbix URL), the status colors are fixed. Existing configurations and stored tokens are migrated automatically.
- Language selection as a drop-down list, **System** preselected.
- Settings, problems, suppress and about dialogs are standard windows; the settings window has a fixed size.

## 1.3.0 – 2026-10-28

### Added

- User interface in English, German and Czech. By default the Windows display language is used, English if it is not supported; a fixed language can be chosen in the settings.
- Optional code signing of the release files with your own certificate, see [Code signing](docs/code-signing.md).

### Changed

- English README.

## 1.2.2 – 2026-10-28

### Security

- Removing old tokens only deletes entries in the Credential Manager that were created by the application.
- The Zabbix URL must be an absolute `https://` or `http://` address; saving an `http://` URL asks for confirmation, because the token would be sent unencrypted.
- Turning off certificate validation asks for confirmation.
- Requests time out after 15 seconds instead of 100 seconds.

### Fixed

- `config.json` is written via a temporary file, so a crash cannot leave a half-written file. An unreadable file is kept as `config.corrupt.json` before the defaults are used.

### Added

- Release workflow that builds the application and publishes it as a GitHub release.

## 1.2.1 – 2026-09-04

### Fixed

- Tray tooltip delay and refresh after alarm actions.

## 1.2.0 – 2026-09-04

### Added

- Acknowledging and suppressing problems directly in the problems window.

## 1.1.3 – 2026-08-18

### Changed

- Problems with ignored severities are filtered out.

## 1.1.2 – 2026-08-18

### Fixed

- Problems of disabled triggers are no longer shown.
- Smaller release package.

## 1.1.0 – 2026-06-23

- First release with tray status, tooltip, problems window and settings.
