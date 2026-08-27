# Changelog

## 1.2.1

- **Fixed**: `Briefing.Language: nl` printed the date header (weekday/month) in English. Alpine's
  `icu-libs` package alone ships ICU with no real locale/calendar data - `CultureInfo.GetCultureInfo`
  "succeeds" for `nl-NL` but `DateTime.ToString` still renders English names, silently, no exception.
  Added `icu-data-full` to the Dockerfile, which carries the actual CLDR data non-English formatting
  needs. Confirmed the app-level code was already correct by reproducing the same formatting call
  outside the container, where it renders correctly.

## 1.2.0

- Added MQTT discovery: when a broker is available in Home Assistant (e.g. the Mosquitto add-on), this
  add-on now publishes a "Receipt Printer Service" device with two buttons (print daily briefing, check
  to-dos now), a `notify` entity (print any text), and a "printer reachable" binary sensor - no YAML
  needed, entities just show up. Broker connection details come from Supervisor's Services API, not
  user-entered config. Turn it off with the new `Mqtt.Enabled: false` option; the HTTP endpoints are
  unaffected either way. New optional add-on dependency: `services: [mqtt:want]` (soft - the add-on
  still runs fine with no broker configured).
- Added `IReceiptPrinter.PingAsync()` (network: hits `/health`; serial: checks the COM port is still
  enumerated) backing the new reachable sensor, without ever sending a probe print.
- **Breaking**: removed the `Location` option entirely - the weather widget now reads Home Assistant's
  own configured latitude/longitude via `/api/config` instead (needs `HomeAssistant.BaseUrl`/`Token`, or
  the add-on's Supervisor token). The city name is also gone from the printed weather line.
- **Breaking**: the add-on no longer exposes `HomeAssistant.BaseUrl`/`Token` as Configuration-tab
  options - it always uses Supervisor's own token now. (Still present as CLI/Service config for running
  standalone outside Home Assistant.)
- **Breaking**: removed `BriefingScheduler` and the `ScheduledBriefingEnabled`/`ScheduledHour`/
  `ScheduledMinute` options - trigger the briefing from a Home Assistant automation instead (HTTP
  `rest_command` or the MQTT button). Nothing in the Service runs on an internal schedule anymore.

## 1.1.2

- **Fixed**: `SUPERVISOR_TOKEN` (and everything else `homeassistant_api: true` was supposed to unlock) was
  never actually reaching the app. The container's `CMD` execs `dotnet` directly, but s6-overlay (the
  base image's init system) only exports its `cont-init.d`-set env vars - including `SUPERVISOR_TOKEN` -
  into processes launched through `with-contenv`. `/diag/home-assistant` (added in 1.1.1) confirmed
  `supervisorTokenPresent: false` despite the permission being granted; wrapping the `CMD` in
  `with-contenv` fixes it.

## 1.1.1

- Added a `GET /diag/home-assistant` endpoint that reports whether Home Assistant connectivity can be
  resolved (explicit `BaseUrl`/`Token` vs. Supervisor's `SUPERVISOR_TOKEN`), without ever exposing the
  actual token value. Useful when the briefing comes back with calendar/to-do/energy sections empty -
  each widget fails silently and just logs to the console, so this pinpoints whether the problem is
  "no connection at all" vs. something failing inside an individual widget's request.

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
