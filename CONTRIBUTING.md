# Contributing

Thank you for helping to improve Zabbix Monitor. Bug reports, ideas, translations and pull requests are welcome.

## Before you start

- **Security vulnerabilities:** do not open a public issue, report them privately as described in [SECURITY.md](SECURITY.md).
- **Questions and problems with the setup:** check the built-in help (tray menu → **Help**), [Troubleshooting](docs/troubleshooting.md) and [Zabbix setup](docs/zabbix-setup.md) first.
- **Existing issues:** search the [issues](https://github.com/petrsykora/ZabbixTrayMonitor/issues) to avoid duplicates.

## Reporting a bug

Open a [bug report](https://github.com/petrsykora/ZabbixTrayMonitor/issues/new?template=bug_report.yml) and include:

- the version of Zabbix Monitor (**About** window) and how it was installed (setup.exe, MSI, ZIP)
- the Windows version and the Zabbix server version
- the steps to reproduce, what you expected and what happened
- the exact error message, if there is one

Never post your API token, and remove internal host names or URLs from screenshots and logs if they must not be public.

## Suggesting a feature

Open a [feature request](https://github.com/petrsykora/ZabbixTrayMonitor/issues/new?template=feature_request.yml) and describe the problem you want to solve, not only the solution. For larger changes, please discuss them in an issue before you start working on a pull request.

## Pull requests

1. Fork the repository and create a branch from `master`.
2. Make your change. Keep it focused on one topic; unrelated clean-ups belong in a separate pull request.
3. Follow the existing code style. Comments in the code are mostly in German, user-facing texts are in the resource files.
4. If you add or change user-facing texts, update **all three** languages (`Resources/Strings.resx`, `Strings.de.resx`, `Strings.cs.resx`) and regenerate `Strings.Designer.cs`, see [Development](docs/development.md#translations).
5. Update the built-in help, README or `docs/` if the behavior changes, and add an entry to [CHANGELOG.md](CHANGELOG.md).
6. Build and test the application on Windows (`dotnet build`, then run it against a Zabbix server).
7. Open the pull request and describe what changed and how you tested it.

Pull requests are merged with **Squash and merge**, so each pull request becomes one commit on `master`.

Building, the installers and the release process are described in [Development](docs/development.md).

## Translations

Corrections of existing translations and new languages are welcome. How to add a language is described in [Development](docs/development.md#adding-a-language).

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE) of this project.

## Code of conduct

Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).
