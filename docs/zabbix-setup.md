# Zabbix setup

Zabbix Monitor reads the current problems through the Zabbix JSON-RPC API with an API token. This page describes how to prepare a Zabbix user and a token with as few permissions as possible.

## Requirements

- Zabbix **6.4 or newer**: the application sends the token in the `Authorization: Bearer` header, which older versions do not support.
- Suppressing problems needs Zabbix 6.2 or newer; acknowledging works with all supported versions.
- The Zabbix web interface must be reachable from the computers via HTTPS. The API path `/api_jsonrpc.php` is added automatically; if Zabbix runs in a subdirectory, include it in the Zabbix URL (for example `https://example.com/zabbix`).

## API methods used

| Method | Purpose |
|---|---|
| `apiinfo.version` | **Test** button in the settings (needs no token) |
| `problem.get` | current problems |
| `event.get` | event details |
| `trigger.get` | host names and trigger/item information |
| `event.acknowledge` | acknowledging and suppressing problems |

## 1. User role

**Users → User roles → Create user role** (or clone an existing one):

- **User type:** User
- **Access to UI elements:** can all be disabled; the application only uses the API
- **API access:** Enabled
- **API methods:** **Allow list** with the methods from the table above
- **Access to actions:**
  - only showing problems: disable all actions
  - with alarm handling: enable **Acknowledge problems**, **Add problem comments** and **Suppress problems**

## 2. User group and host permissions

**Users → User groups → Create user group**:

- **Frontend access:** Disabled is enough for API-only users; keep the default if people also sign in to the web interface with this user
- **Host permissions** (Zabbix 7.x) or **Permissions** (Zabbix 6.x): **Read** for the host groups that should be monitored

Problems of hosts the user cannot read are not returned and therefore not shown.

## 3. User

Use a dedicated user per person (recommended) or a shared technical user:

**Users → Users → Create user**, add it to the user group from step 2 and assign the role from step 1.

With a user per person, acknowledgements and suppressions in Zabbix show who handled the problem. The default acknowledgement message also contains the Windows user name.

## 4. API token

Create the token as the user (**User settings → API tokens → Create API token**) or as an administrator for another user (**Users → API tokens → Create API token**):

- **Name:** for example `Zabbix Monitor – PC-0123`
- **Expires at:** recommended; when the token expires, the tray icon turns red with an authorization error and the user has to enter a new token
- copy the token right after creating it, Zabbix shows it only once

Enter the token in Zabbix Monitor under **Settings → API token**. It is stored in the Windows Credential Manager, never in `config.json`.

## 5. Check the token

The **Test** button in the settings only checks that the Zabbix API can be reached (`apiinfo.version`); it does not check the token. To check the token and its permissions before rolling out, call `problem.get` in PowerShell:

```powershell
$body = '{"jsonrpc":"2.0","method":"problem.get","params":{"countOutput":true},"id":1}'
Invoke-RestMethod -Method Post -Uri "https://zabbix.example.com/api_jsonrpc.php" `
  -ContentType "application/json-rpc" `
  -Headers @{ Authorization = "Bearer <token>" } `
  -Body $body
```

- `result` with a number: the token works; the number is the count of problems the user can see.
- `error` with `Not authorized`: the token is wrong, expired or disabled.
- `error` with `No permissions to call ...`: the method is missing in the allow list of the user role.

After saving the token in the application, **Refresh** in the tray menu shows the result right away.

## Security notes

- Always use `https://`. The token is sent with every request.
- Do not use **Ignore SSL certificate errors** in production. If the Zabbix server uses a certificate from an internal certification authority, distribute the CA certificate to the computers (for example via Group Policy) instead.
- Give the user only read permissions on the needed host groups. Acknowledging and suppressing are controlled by the role actions, not by write permissions.
- Revoke the token in Zabbix when a computer is decommissioned or a user leaves.
