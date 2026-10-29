# Security Policy

## Supported versions

Security fixes are made for the latest release only. Please update to the newest version from the [releases](https://github.com/petrsykora/ZabbixTrayMonitor/releases) before reporting a problem.

| Version          | Supported |
| ---------------- | --------- |
| Latest release   | Yes       |
| Older releases   | No        |

## Reporting a vulnerability

Please do **not** report security vulnerabilities in public issues, pull requests or discussions.

Report them privately through GitHub instead:

1. Open the [Security tab](https://github.com/petrsykora/ZabbixTrayMonitor/security) of this repository.
2. Click **Report a vulnerability**.
3. Describe the problem, the affected version, the steps to reproduce it and the possible impact.

You will receive an answer through the same private report. Once a fix is available, it is published in a new release, and the report can be disclosed together with the release notes. Please give us reasonable time to fix the problem before you disclose it publicly.

## Scope

In scope are problems in this application and its release packages, for example:

- leaking or exposing the Zabbix API token
- sending the token to an unexpected host or over an unprotected connection
- weaknesses in the MSI installer or the release workflow
- deleting or changing data that does not belong to the application (for example other entries in the Windows Credential Manager)

Out of scope:

- vulnerabilities in Zabbix itself; please report them to [Zabbix](https://www.zabbix.com/security_advisories)
- problems that require `IgnoreCertificateErrors` to be enabled, since this option deliberately turns off certificate validation
- problems that require an attacker who already has administrator rights or full access to the user's Windows account

## How the application handles sensitive data

- The Zabbix API token is stored in the Windows Credential Manager for the current Windows user, never in `config.json`.
- The token is only sent to the configured Zabbix URL as a bearer token. Use `https://`; the application asks for confirmation before saving an `http://` URL.
- Besides the Zabbix server, the application only connects to `api.github.com` once a day to check for a new version. It sends no token and no data from Zabbix, and it never downloads or runs installers by itself. The check can be turned off in the settings or for all users by policy, see [Deployment](docs/deployment.md#update-notification).
- Certificate validation is on by default. `IgnoreCertificateErrors` turns it off and should only be used for testing.
- Use a dedicated Zabbix user for the token with as few permissions as possible: read access to the monitored host groups and, if alarm handling is used, permission to acknowledge and suppress problems.

Further security notes for users are in the built-in help (**Help → Security**) and in the [README](README.md#security-notes).
