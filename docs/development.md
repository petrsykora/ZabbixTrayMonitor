# Development

## Requirements

- .NET 10 SDK
- Windows 10/11 to run the application and to build the installers
- Visual Studio 2026 or another editor; Visual Studio regenerates `Strings.Designer.cs` automatically (see [Translations](#translations))

The application project can also be restored and built on Linux or macOS (`EnableWindowsTargeting` is set), for example for CI checks, but it only runs on Windows.

## Repository layout

| Path | Content |
|---|---|
| `App.xaml`, `MainWindow.xaml` | application start, tray icon and tray menu |
| `Views/` | settings, problems, suppress, help and about windows |
| `Services/` | Zabbix API client, configuration, Credential Manager, localization, theme and font scaling |
| `Models/` | configuration and problem models |
| `Resources/` | translations (`Strings*.resx`) and the generated `Strings.Designer.cs` |
| `Assets/` | application and tray icons, README screenshots |
| `installer/msi/` | MSI installer (WiX Toolset v5), variants `SelfContained` and `RuntimeDependent` |
| `installer/bundle/` | `setup.exe` (WiX Burn bundle) that installs the .NET runtime if needed |
| `tools/` | scripts for code signing, VirusTotal check, license text and icon generation |
| `winget/manifests/` | local winget manifests |
| `.github/workflows/` | release and winget workflows |

## Build and run

```powershell
dotnet build ZabbixTrayMonitor.csproj
dotnet run --project ZabbixTrayMonitor.csproj
```

The application uses the same settings and token as an installed version (`%AppData%\ZabbixTrayMonitor`). Exit an installed instance before starting a debug build, otherwise two tray icons run side by side.

## Publish

Self-contained (runtime included), as in the release:

```powershell
dotnet publish ZabbixTrayMonitor.csproj -c Release -r win-x64 --self-contained true -o publish
```

Runtime-dependent:

```powershell
dotnet publish ZabbixTrayMonitor.csproj -c Release -r win-x64 --self-contained false -o publish-runtime-dependent
```

## Build the MSI

The license dialog needs `installer\License.rtf`, which is generated from `LICENSE` and not committed:

```powershell
.\tools\New-LicenseRtf.ps1 -Source LICENSE -Destination installer\License.rtf
```

Then, after publishing as above:

```powershell
dotnet build installer\msi\ZabbixTrayMonitor.Installer.wixproj -c Release -p:ProductVersion=1.3.7 -p:PublishDir="$PWD\publish"
dotnet build installer\msi\ZabbixTrayMonitor.Installer.wixproj -c Release -p:ProductVersion=1.3.7 -p:InstallerVariant=RuntimeDependent -p:PublishDir="$PWD\publish-runtime-dependent"
```

The MSI files are written to `installer\msi\bin\Release\SelfContained\` and `installer\msi\bin\Release\RuntimeDependent\`.

`setup.exe` needs the download URL, size, SHA-512 hash and version of the current .NET Desktop Runtime installer. The **Build setup.exe** step in `.github/workflows/release.yml` determines them from Microsoft's release metadata and shows how to call `installer\bundle\ZabbixTrayMonitor.Bundle.wixproj`.

The WiX Toolset is restored as a NuGet SDK; nothing has to be installed. Major updates of WiX (v6 and newer) are excluded from Dependabot on purpose, because they come with changed license terms.

## Translations

| File | Language |
|---|---|
| `Resources/Strings.resx` | English (neutral language and fallback) |
| `Resources/Strings.de.resx` | German |
| `Resources/Strings.cs.resx` | Czech |

XAML uses the texts with `{x:Static res:Strings.Key}`, code with `Strings.Key`.

### Changing or adding a text

1. Add or change the entry in **all three** `.resx` files with the same name.
2. Regenerate `Resources/Strings.Designer.cs`: Visual Studio does it when `Strings.resx` is saved. Without Visual Studio, in a Developer PowerShell:

   ```powershell
   resgen Resources\Strings.resx Resources\Strings.resources /str:cs,ZabbixTrayMonitor.Resources,Strings,Resources\Strings.Designer.cs /publicClass
   Remove-Item Resources\Strings.resources
   ```

3. Commit `Strings.Designer.cs` together with the `.resx` files. The WPF build needs the committed file.

### Adding a language

1. Copy `Resources/Strings.resx` to `Resources/Strings.<code>.resx` (for example `Strings.fr.resx`) and translate the values.
2. Add the code to `LocalizationService.SupportedLanguages` (`Services/LocalizationService.cs`).
3. Add the language to the language list in `Views/ZabbixConfigWindow.xaml.cs` (method that fills `LanguageComboBox`), with the name of the language in that language.
4. Add the language to the README (features, badge, languages section).

## Releasing a new version

1. Update the version in **both** files:
   - `Directory.Build.props`: `Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`
   - `ZabbixTrayMonitor.csproj`: the same four properties (the release workflow reads the version from here)
2. Add the changes to [CHANGELOG.md](../CHANGELOG.md).
3. Update the version in the examples in README and docs if they mention it.
4. Optionally add winget manifests for the new version in `winget/manifests/p/PetrSykora/ZabbixMonitor/<version>/`.
5. Commit and push to `master`.
6. Start the release: **Actions → Release → Run workflow**, branch `master`, tag `v<version>` (for example `v1.3.7`). The workflow creates the tag on the current commit and the GitHub release with all five files. Pushing a tag `v*` starts the same workflow.

Use **dry_run** to only build: the files are then attached to the workflow run as artifacts and no tag or release is created.

The workflow signs the files when the signing secrets are set, see [Code signing](code-signing.md). When the `VIRUSTOTAL_API_KEY` secret is set, it checks the files with VirusTotal, see [VirusTotal](#virustotal). When the `WINGET_TOKEN` secret is set and the package already exists in the official winget catalog, it also submits the new version to `microsoft/winget-pkgs`.

## VirusTotal

After the release is created, the release workflow can upload all five files to [VirusTotal](https://www.virustotal.com) (`tools/Submit-VirusTotal.ps1`), wait for the analyses and then:

- add a **VirusTotal** section to the release notes with the detections and a link to the report of every file
- attach `virustotal-badge.json` to the release and, for the latest release, store it in the branch `badges`, from where the badge in the README reads it (shields.io does not load files from GitHub release downloads). The branch always has a single commit and is overwritten by every check.

### Setup

1. Create a free account on <https://www.virustotal.com> and copy the **API key** from your profile.
2. Add it in the repository under **Settings → Secrets and variables → Actions → New repository secret** as `VIRUSTOTAL_API_KEY`.
3. The next release is checked. Without the secret the step is skipped.

To check an existing release (for example one released before the secret was set) or to refresh its result, run **Actions → VirusTotal → Run workflow** with the tag, for example `v1.3.7`. It downloads the files of that release, checks them and replaces the VirusTotal section of the release notes and the badge file.

The free public API allows 4 requests per minute and is meant for non-commercial use. The script waits between requests, so the check takes about 5 to 15 minutes. If VirusTotal fails or takes longer than 30 minutes, the step fails, but the release itself stays as it is. Files uploaded to VirusTotal can be downloaded by its subscribers, which is fine for public releases.

### Badge

The badge in the README shows the result of the latest release (green without detections, orange for 1 to 3 detections, red for more):

```markdown
[![VirusTotal](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Fpetrsykora%2FZabbixTrayMonitor%2Fbadges%2Fvirustotal-badge.json)](https://github.com/petrsykora/ZabbixTrayMonitor/releases/latest)
```

Unsigned .NET applications, especially the self-contained variants, are sometimes reported by a few engines as a false positive. Signing the files (see [Code signing](code-signing.md)) usually reduces that.

## Security checks

- **CodeQL** (default setup) analyzes C#, Python and the workflows on every push.
- **Dependabot** checks NuGet packages (application and installers) and GitHub Actions weekly, see `.github/dependabot.yml`.
- Vulnerabilities are reported privately, see [SECURITY.md](../SECURITY.md).
