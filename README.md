# Woosim Receipt Printer

Reviving an old Woosim serial thermal receipt printer (salvaged from a photobooth) and hooking it up to a daily briefing / to-do printout - date, weather, calendar, to-do list, and yesterday's energy usage - driven by Home Assistant, with a longer-term goal of running the printer standalone on an ESP32 over WiFi instead of a PC.

This repository doubles as a Home Assistant **add-on repository** - see [Running as a Home Assistant add-on](#running-as-a-home-assistant-add-on).

## Status

- [x] Printer talks over RS-232 (COM3, 9600 baud) via a USB-to-serial adapter.
- [x] Receipts are built as plain data (a `Receipt` of `IElement`s) and handed to an `IReceiptPrinter` - callers never touch ESC/POS commands or connection state directly.
- [x] Daily briefing printout: date, weather, calendar (today + upcoming), a to-do list, and yesterday's energy usage (solar/grid/gas) - each section is a self-contained "widget" behind `IBriefingWidget`. Language (Dutch/English) and which widgets run (and in what order) are configurable at runtime - see [Configuration](#configuration). Triggered on demand from a Home Assistant automation over MQTT rather than on an internal schedule.
- [x] To-do list sourced from Apple Reminders, via an iOS Shortcut pushing to a Home Assistant webhook (CalDAV can't see most real reminders lists - see [the data flow section](#to-do-list-data-flow)).
- [x] Calendar events sourced from Home Assistant's `caldav` integration (iCloud calendar).
- [x] Energy usage (solar production, grid import/export, gas) pulled straight from Home Assistant's long-term statistics (the same data behind the Energy dashboard), via its WebSocket API.
- [x] `ReceiptPrinter.Service` - MQTT-triggered (no HTTP API) and a "new to-do -> print its own note" checker, both triggered from Home Assistant automations rather than an internal scheduler - runs anywhere on the network (e.g. as this repo's Home Assistant add-on) and prints to `ReceiptPrinter.NetworkSerialService`, which stays on the PC with the printer wired up over serial.
- [ ] Move the printer off the PC onto a standalone ESP32 (parts ordered - see [docs/HARDWARE.md](docs/HARDWARE.md)). `NetworkWoosimPrinter`/`NetworkSerialService` already speak the wire protocol the ESP32 firmware will need to speak too.

## Project layout

A multi-project solution ([`src/ReceiptPrinter.slnx`](src/ReceiptPrinter.slnx)) split by concern, so callers (the CLI, the Service) don't need to know printer-specific details, and any printer transport can plug in behind the same contract:

- **[`ReceiptPrinter.Shared`](src/ReceiptPrinter.Shared)** - shared library referenced by everything else, organized by namespace/folder:
  - [`Receipts/`](src/ReceiptPrinter.Shared/Receipts) (`ReceiptPrinter.Receipts`) - the receipt data model: `IElement`/`TextElement`/`Receipt`/`CutStyle`/`Justification`, the `IReceiptPrinter` contract (`Task PrintAsync(Receipt receipt)`, `Task<bool> PingAsync()`), and [`ReceiptMarkdown`](#custom-print-formatting) (a tiny text-formatting dialect for freeform print requests). A `TextElement` fully describes its own formatting, so nothing needs to track "current printer state" between elements. `IElement` is JSON-polymorphic (`[JsonDerivedType]`) so a `Receipt` can round-trip as JSON - used when `ReceiptPrinter.Service` POSTs one to `ReceiptPrinter.NetworkSerialService`.
  - [`Widgets/`](src/ReceiptPrinter.Shared/Widgets) (`ReceiptPrinter.Widgets`) - `IBriefingWidget` (a widget fetches its own data and returns the elements for its section), its implementations (`DateHeaderWidget`, `WeatherWidget`, `CalendarWidget`, `TodoWidget`, `EnergyWidget`), and `DailyBriefingWidget`, itself an `IBriefingWidget` that combines whichever of the others `Briefing.Widgets` configures into one - `DailyBriefing.BuildAsync` is the thin top-level entry point wrapping that as a printable `Receipt`.
  - [`HomeAssistant/`](src/ReceiptPrinter.Shared/HomeAssistant) (`ReceiptPrinter.HomeAssistant`) - pulls data from Home Assistant: `HomeAssistantTodos`/`HomeAssistantCalendar` over REST, `HomeAssistantEnergy` over its WebSocket API (long-term statistics have no REST equivalent).
  - [`Configuration/`](src/ReceiptPrinter.Shared/Configuration) (`ReceiptPrinter.Configuration`) - `ReceiptPrinterOptions` (the whole app's settings, bound from `IConfiguration` - see [Configuration](#configuration) below) and `ReceiptPrinterConfiguration` (builds that `IConfiguration` identically for the CLI and the Service), plus `Localization` (the NL/EN string table + culture the widgets render with) and `TodoFile`/`ConfigPaths` (the to-do.txt fallback and where it/runtime state live - `RECEIPTPRINTER_CONFIG_DIR` when set, e.g. the add-on's persistent `/data`).
- **[`ReceiptPrinter.Serial`](src/ReceiptPrinter.Serial)** (`ReceiptPrinter.Printers.Serial`) - `SerialWoosimPrinter`, the ESC/POS driver actually driving the printer today, translating `Receipt` elements into bytes over a serial port.
- **[`ReceiptPrinter.Network`](src/ReceiptPrinter.Network)** (`ReceiptPrinter.Printers.Network`) - `NetworkWoosimPrinter`, POSTs a `Receipt` as JSON to `http://{host}/print`. Today that hits `ReceiptPrinter.NetworkSerialService`; once the ESP32 firmware exists it can speak the same protocol and this class won't need to change.
- **[`ReceiptPrinter.NetworkSerialService`](src/ReceiptPrinter.NetworkSerialService)** - a tiny HTTP service wrapping `SerialWoosimPrinter` behind the wire protocol `NetworkWoosimPrinter` expects (`POST /print`). Runs on the PC with the printer wired up over serial, standing in for the not-yet-built ESP32 firmware.
- **[`ReceiptPrinter.CLI`](src/ReceiptPrinter.CLI)** (`ReceiptPrinter.Cli`) - the console app for manual use: `test`/`briefing` commands, built on [`System.CommandLine`](https://www.nuget.org/packages/System.CommandLine).
- **[`ReceiptPrinter.Service`](src/ReceiptPrinter.Service)** - the MQTT-triggered service, see below.
- `docs/HARDWARE.md` - the hardware plan for moving the printer onto an ESP32.
- `ref/` - vendor manuals, old SDKs, and a ~10-year-old C# project for this same printer (git-ignored, kept locally only).

## Running the CLI

```bash
cd src/ReceiptPrinter.CLI

dotnet run -- --help                          # full command/option reference
dotnet run -- test                            # basic ESC/POS test print, serial/COM3/9600 by default
dotnet run -- test --printer serial --port COM3 --baud 9600   # same, explicit
dotnet run -- test --printer network --host printer-pc.local:5251  # over the network, via ReceiptPrinter.NetworkSerialService

dotnet run -- briefing                      # the daily briefing, serial by default (same --printer/--port/--baud/--host options as test)

dotnet run -- print "**Milk**, eggs, bread"          # ReceiptMarkdown text as an argument
printf "# Grocery run\n**Milk**, eggs, bread\n" | dotnet run -- print   # ...or piped via stdin, if no argument is given
```

Both the CLI and the Service read settings from the same place - see [Configuration](#configuration).

## Configuration

Everything (printer transport, Home Assistant, the briefing itself) is a single `ReceiptPrinterOptions` tree bound from `IConfiguration` (see [`ReceiptPrinterConfiguration.cs`](src/ReceiptPrinter.Shared/Configuration/ReceiptPrinterConfiguration.cs)), layered the same way for the CLI and the Service:

1. `appsettings.json` next to the executable - committed, safe non-secret defaults (widget order, etc.).
2. `appsettings.local.json` next to it (or under `RECEIPTPRINTER_CONFIG_DIR`, if set) - **git-ignored**, for your actual Home Assistant token when running outside Home Assistant.
3. `/data/options.json` - only present inside the Home Assistant add-on, written by Supervisor from its Configuration tab; reloads live, no restart needed.
4. Environment variables (e.g. `HomeAssistant__Token=...`, double-underscore for nesting) - highest priority, useful for CI/containers.

The sections, matching `appsettings.json`'s layout 1:1:

- **`Printer`** - `Type` (`serial`/`network`), `Port`/`Baud` (serial), `NetworkHost` (network - `ReceiptPrinter.NetworkSerialService`'s `host:port`).
- **`HomeAssistant`** - `TodoEntityId`/`TodoAttributeName` for the to-do list, and the entity IDs feeding your Energy dashboard (`SolarProductionEntityId`, `GridImportEntityIds`/`GridExportEntityIds` - lists, summed for multi-tariff meters -, `GasEntityId`). Also where the weather widget's coordinates come from - it reads Home Assistant's own configured latitude/longitude via `/api/config`, so there's no separate location setting anywhere. `BaseUrl`/`Token` only exist for running the CLI/Service standalone, outside Home Assistant, with a personal long-lived access token - **the add-on doesn't expose them at all**, it reaches Home Assistant through Supervisor's proxy using its own automatically-injected token instead (see [Running as a Home Assistant add-on](#running-as-a-home-assistant-add-on)).
- **`Briefing`** - `Language` (`Nl`/`En` - translates every widget's labels via [`Localization.cs`](src/ReceiptPrinter.Shared/Configuration/Localization.cs)), `Widgets` (which to run and in what order - valid names `DateHeader`/`Weather`/`Calendar`/`Todo`/`Energy`, empty/omitted defaults to all five), and `TodoNotesEnabled` (the Service's to-do-note checker). There's no internal schedule - trigger the briefing from a Home Assistant automation instead (the MQTT button - see [MQTT entities](#mqtt-entities)).

`todo.txt` (plain-text fallback to-do list, used only if Home Assistant isn't configured) and `todo-note-store.json` (the Service's persisted "already printed" tracking) stay plain files next to the executable - they're free-form content/runtime state, not settings.

## `ReceiptPrinter.Service`

A plain background worker - no HTTP server at all, every action is triggered over MQTT (see below). It's been run against the real printer, but is still light on production hardening (no reconnect logic if the printer connection drops).

- `MqttAddonService` (hosted service) subscribes to the MQTT command topics backing the entities below and does the actual printing.
- The to-do note checker: compares the current to-do list (same source as the briefing's `TodoWidget`) against a small persisted store (`todo-note-store.json`) of what's already been printed. Anything new gets its own little note printed - a `TODO` heading, the item text, and the date (`dd-MM-yyyy`) it was printed. Anything that's dropped out of the source (presumably finished and thrown away) is just forgotten, no reprint. A no-op if `Briefing:TodoNotesEnabled` is `false`.
- On startup, logs whether Home Assistant connectivity resolved (and from where), without ever exposing the token value - the first thing to check in the add-on's log if the briefing comes back with sections missing.

Run it (from `src/ReceiptPrinter.Service`) with `dotnet run`. By default it prints with `Printer:Type=network`, i.e. it expects `ReceiptPrinter.NetworkSerialService` to be reachable at `Printer:NetworkHost`.

## MQTT entities

Add-ons can't register real Home Assistant services/actions directly - only a custom integration can, and there's no HTTP API either - so **MQTT discovery** is the only way in. `config.yaml` declares `services: [mqtt:need]`, so a broker (e.g. the official Mosquitto add-on) is required. [`MqttAddonService`](src/ReceiptPrinter.Service/Mqtt/MqttAddonService.cs) resolves it via Supervisor's Services API (`SUPERVISOR_TOKEN`, no user-entered broker config needed) and publishes retained discovery configs for a single "Receipt Printer Service" device:

| Entity | Behavior |
|---|---|
| `button.receipt_printer_print_daily_briefing` | Builds and prints the daily briefing |
| `button.receipt_printer_check_to_dos_now` | Runs the to-do note checker |
| `notify.receipt_printer_print` | `notify.send_message` prints the message - see [`ReceiptMarkdown`](#custom-print-formatting) below for formatting |
| `binary_sensor.receipt_printer_printer_reachable` | Polled every minute via `IReceiptPrinter.PingAsync()` - never prints anything |

An automation just looks like:

```yaml
- action: button.press
  target:
    entity_id: button.receipt_printer_print_daily_briefing
```

No hardcoded add-on hostname, no YAML beyond the automation itself - but if no MQTT broker is configured in Home Assistant at all, the add-on has nothing to do (it logs this and idles).

### Custom print formatting

`notify.receipt_printer_print`'s message goes through [`ReceiptMarkdown`](src/ReceiptPrinter.Shared/Receipts/ReceiptMarkdown.cs), a tiny receipt-specific dialect - just enough to make a printed note look intentional, entirely in the message string (no `data:` fields, no JSON):

- A line that's just `~~~` requests a full cut instead of the default partial one, and prints nothing for that line.
- A line that's just `[WidgetName]` (e.g. `[Weather]`, `[Calendar]`) splices in that briefing widget's own live output - the same widgets/factories the daily briefing itself uses, including `[DailyBriefing]` for the whole thing.
- A line starting with `>>` right-justifies; `>` centers; otherwise left (default) - checked before the heading marker, so `> # Heading` is a centered heading.
- A line starting with `# ` prints big and bold (e.g. `# Maandag` as a heading).
- `**bold**` and `*underline*` toggle for the enclosed text - freely mixable/nestable, multiple times per line.
- `\*`, `\#`, `\>`, `\~`, `\\` escape to a literal character.

```yaml
- action: notify.send_message
  target:
    entity_id: notify.receipt_printer_print
  data:
    message: |-
      # Grocery run
      **Milk**, eggs, bread
      *don't forget the receipt*
      [Weather]
      ~~~
```

## Running as a Home Assistant add-on

The Service doesn't need to run on the same machine as the printer - it just needs network access to `ReceiptPrinter.NetworkSerialService`:

```
Home Assistant (add-on)                    PC (has the printer wired up over serial)
ReceiptPrinter.Service      --HTTP-->      ReceiptPrinter.NetworkSerialService --serial--> Woosim printer
(MQTT-triggered,                           (forwards Receipt JSON straight to
 HA polling, TODO-note checker)             SerialWoosimPrinter)
```

- On the PC: `cd src/ReceiptPrinter.NetworkSerialService && dotnet run` - listens on `http://0.0.0.0:5251` by default (see its `appsettings.json` for the serial port/baud).
- A broker set up in Home Assistant (e.g. the official Mosquitto add-on) - required, since MQTT is the only way to trigger this add-on at all (`config.yaml` declares `services: [mqtt:need]`).
- In Home Assistant: **Settings -> Add-ons -> Add-on Store -> ⋮ -> Repositories**, add `https://github.com/ikkentim/woosim-printer`, then install "Receipt Printer Service" from the store. Its **Configuration** tab exposes the `Printer`/`HomeAssistant`/`Briefing` groups described in [Configuration](#configuration) above - set `Printer.NetworkHost` to the PC's `host:port` from above. There's no `BaseUrl`/`Token` to fill in and no location to enter: the add-on has `homeassistant_api: true`, so it talks to Home Assistant through Supervisor's proxy with an automatically-scoped token, and reads Home Assistant's own configured coordinates for the weather widget. Then start it, and wire up an automation using the MQTT button/notify entities (see [MQTT entities](#mqtt-entities)) to actually trigger the briefing/to-do check on whatever schedule you want.

The image is built and published to `ghcr.io/ikkentim/ha-{arch}-receiptprinter-service` by [`.github/workflows/builder.yaml`](.github/workflows/builder.yaml) on every push to `main` - Supervisor just pulls the prebuilt image rather than building it on-device (this repo's Dockerfile needs the whole `src/` solution as build context, since `ReceiptPrinter.Service` references its sibling projects). `config.yaml`, `Dockerfile`, `DOCS.md`, `CHANGELOG.md` and `repository.yaml` at the repo root are what make this a valid single-add-on repository - see the [Home Assistant add-on docs](https://developers.home-assistant.io/docs/add-ons) for the format.

This is exactly the same wire protocol (`POST /print`, a JSON `Receipt`) the real ESP32 firmware will need to speak once it exists, so `NetworkSerialService` is a drop-in stand-in until then.

## To-do list data flow

Apple Reminders lists created after Reminders' CloudKit-based redesign don't get exposed over CalDAV, so pulling them server-side isn't possible. Instead:

```
iPhone (Shortcuts: "Find Reminders" -> Combine Text -> Get Contents of URL)
   -> Home Assistant webhook
   -> trigger-based template sensor (sensor.todo_list, "items" attribute - no length cap)
   -> this app polls the HA REST API when printing
```

See [docs/HARDWARE.md](docs/HARDWARE.md#notes--gotchas) for the full story of why this ended up being necessary.

## License

[MIT](LICENSE)
