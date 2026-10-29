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
| `tools/` | scripts for code signing, license text and icon generation |
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
dotnet build installer\msi\ZabbixTrayMonitor.Installer.wixproj -c Release -p:ProductVersion=1.3.4 -p:PublishDir="$PWD\publish"
dotnet build installer\msi\ZabbixTrayMonitor.Installer.wixproj -c Release -p:ProductVersion=1.3.4 -p:InstallerVariant=RuntimeDependent -p:PublishDir="$PWD\publish-runtime-dependent"
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
6. Start the release: **Actions → Release → Run workflow**, branch `master`, tag `v<version>` (for example `v1.3.4`). The workflow creates the tag on the current commit and the GitHub release with all five files. Pushing a tag `v*` starts the same workflow.

Use **dry_run** to only build: the files are then attached to the workflow run as artifacts and no tag or release is created.

The workflow signs the files when the signing secrets are set, see [Code signing](code-signing.md). When the `WINGET_TOKEN` secret is set and the package already exists in the official winget catalog, it also submits the new version to `microsoft/winget-pkgs`.

## Security checks

- **CodeQL** (default setup) analyzes C#, Python and the workflows on every push.
- **Dependabot** checks NuGet packages (application and installers) and GitHub Actions weekly, see `.github/dependabot.yml`.
- Vulnerabilities are reported privately, see [SECURITY.md](../SECURITY.md).
