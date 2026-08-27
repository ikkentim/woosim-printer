# Changelog

## 1.1.0

- **Breaking**: replaced the flat `printer_network_host` add-on option and the file-based
  `ha-config.json`/`reminders-config.json`/`briefing-config.json`/`briefing-settings.json` config with
  nested add-on options matching the app's `IConfiguration` structure directly: `Printer`, `Location`,
  `HomeAssistant`, `Briefing`. Re-enter your settings under the new option groups after updating.
- Removed the direct Apple Reminders CalDAV client (`AppleReminders.cs`, `Reminders` config) - it never
  worked reliably in practice. The to-do list is Home Assistant + `todo.txt` only now.
- Added `homeassistant_api: true`, so the add-on can talk to Home Assistant through Supervisor's proxy
  using its own automatically-injected token - leave `HomeAssistant.BaseUrl`/`Token` empty to use this
  instead of a personal long-lived access token.

## 1.0.1

- Pin the HTTP port via `--urls http://+:8099` on the container's `dotnet` command instead of relying
  solely on the `ASPNETCORE_URLS` env var, which could get shadowed by the base image's entrypoint
  chain - it was observed listening on the ASP.NET Core default (`http://localhost:5000`) instead of
  the port `config.yaml` actually publishes.

## 1.0.0

- Initial add-on release: HTTP API (`/print`, `/briefing/trigger`, `/todos/check`) and a daily scheduled
  briefing, printing over the network to `ReceiptPrinter.NetworkSerialService`.
