# Woosim Receipt Printer

Reviving an old Woosim serial thermal receipt printer (salvaged from a photobooth) and hooking it up to a daily briefing / to-do printout - date, weather, calendar, to-do list, and yesterday's energy usage - driven by Home Assistant, with a longer-term goal of running the printer standalone on an ESP32 over WiFi instead of a PC.

This repository doubles as a Home Assistant **add-on repository** - see [Running as a Home Assistant add-on](#running-as-a-home-assistant-add-on).

## Status

- [x] Printer talks over RS-232 (COM3, 9600 baud) via a USB-to-serial adapter.
- [x] Receipts are built as plain data (a `Receipt` of `IElement`s) and handed to an `IReceiptPrinter` - callers never touch ESC/POS commands or connection state directly.
- [x] Daily briefing printout (in Dutch): date, weather, calendar (today + upcoming), a to-do list, and yesterday's energy usage (solar/grid/gas) - each section is a self-contained "widget" behind `IBriefingWidget`.
- [x] To-do list sourced from Apple Reminders, via an iOS Shortcut pushing to a Home Assistant webhook (CalDAV can't see most real reminders lists - see [the data flow section](#to-do-list-data-flow)).
- [x] Calendar events sourced from Home Assistant's `caldav` integration (iCloud calendar).
- [x] Energy usage (solar production, grid import/export, gas) pulled straight from Home Assistant's long-term statistics (the same data behind the Energy dashboard), via its WebSocket API.
- [x] `ReceiptPrinter.Service` - an HTTP API, a scheduled daily briefing, and a "new to-do -> print its own note" checker - runs anywhere on the network (e.g. as this repo's Home Assistant add-on) and prints to `ReceiptPrinter.NetworkSerialService`, which stays on the PC with the printer wired up over serial.
- [ ] Move the printer off the PC onto a standalone ESP32 (parts ordered - see [docs/HARDWARE.md](docs/HARDWARE.md)). `NetworkWoosimPrinter`/`NetworkSerialService` already speak the wire protocol the ESP32 firmware will need to speak too.

## Project layout

A multi-project solution ([`src/ReceiptPrinter.slnx`](src/ReceiptPrinter.slnx)) split by concern, so callers (the CLI, the Service) don't need to know printer-specific details, and any printer transport can plug in behind the same contract:

- **[`ReceiptPrinter.Shared`](src/ReceiptPrinter.Shared)** - shared library referenced by everything else, organized by namespace/folder:
  - [`Receipts/`](src/ReceiptPrinter.Shared/Receipts) (`ReceiptPrinter.Receipts`) - the receipt data model: `IElement`/`TextElement`/`Receipt`/`CutStyle`/`Justification`, and the `IReceiptPrinter` contract (`Task PrintAsync(Receipt receipt)`). A `TextElement` fully describes its own formatting, so nothing needs to track "current printer state" between elements. `IElement` is JSON-polymorphic (`[JsonDerivedType]`) so a `Receipt` can round-trip through the Service's HTTP API.
  - [`Widgets/`](src/ReceiptPrinter.Shared/Widgets) (`ReceiptPrinter.Widgets`) - `IBriefingWidget` (a widget fetches its own data and returns the elements for its section), its implementations (`DateHeaderWidget`, `WeatherWidget`, `CalendarWidget`, `TodoWidget`, `EnergyWidget`), and `DailyBriefing`, which runs them all and assembles the full `Receipt`.
  - [`HomeAssistant/`](src/ReceiptPrinter.Shared/HomeAssistant) (`ReceiptPrinter.HomeAssistant`) - pulls data from Home Assistant: `HomeAssistantTodos`/`HomeAssistantCalendar` over REST, `HomeAssistantEnergy` over its WebSocket API (long-term statistics have no REST equivalent).
  - [`Reminders/`](src/ReceiptPrinter.Shared/Reminders) (`ReceiptPrinter.Reminders`) - `AppleReminders`, a CalDAV client for iCloud Reminders (kept as a fallback/reference; see [docs/HARDWARE.md](docs/HARDWARE.md#notes--gotchas) for why it doesn't see most real reminders lists).
  - [`Configuration/`](src/ReceiptPrinter.Shared/Configuration) (`ReceiptPrinter.Configuration`) - `BriefingConfig` (loads/generates the local config files below) and `ConfigPaths` (resolves where they live - next to the executable by default, or `RECEIPTPRINTER_CONFIG_DIR` when set, e.g. the add-on's persistent `/data`).
- **[`ReceiptPrinter.Serial`](src/ReceiptPrinter.Serial)** (`ReceiptPrinter.Printers.Serial`) - `SerialWoosimPrinter`, the ESC/POS driver actually driving the printer today, translating `Receipt` elements into bytes over a serial port.
- **[`ReceiptPrinter.Network`](src/ReceiptPrinter.Network)** (`ReceiptPrinter.Printers.Network`) - `NetworkWoosimPrinter`, POSTs a `Receipt` as JSON to `http://{host}/print`. Today that hits `ReceiptPrinter.NetworkSerialService`; once the ESP32 firmware exists it can speak the same protocol and this class won't need to change.
- **[`ReceiptPrinter.NetworkSerialService`](src/ReceiptPrinter.NetworkSerialService)** - a tiny HTTP service wrapping `SerialWoosimPrinter` behind the wire protocol `NetworkWoosimPrinter` expects (`POST /print`). Runs on the PC with the printer wired up over serial, standing in for the not-yet-built ESP32 firmware.
- **[`ReceiptPrinter.CLI`](src/ReceiptPrinter.CLI)** (`ReceiptPrinter.Cli`) - the console app for manual use: `test`/`briefing`/`reminders-debug` commands.
- **[`ReceiptPrinter.Service`](src/ReceiptPrinter.Service)** - the HTTP API + scheduler, see below.
- `docs/HARDWARE.md` - the hardware plan for moving the printer onto an ESP32.
- `ref/` - vendor manuals, old SDKs, and a ~10-year-old C# project for this same printer (git-ignored, kept locally only).

## Running the CLI

```bash
cd src/ReceiptPrinter.CLI

# dotnet run -- <command> [printer-type] [printer-args...]
dotnet run -- test                                # basic ESC/POS test print, serial/COM3/9600 by default
dotnet run -- test serial COM3 9600               # same, explicit
dotnet run -- test network printer-pc.local:5251  # over the network, via ReceiptPrinter.NetworkSerialService

dotnet run -- briefing                      # the daily briefing, serial by default
dotnet run -- reminders-debug               # lists Apple Reminders CalDAV lists + contents, for debugging
```

The `briefing` command auto-generates local config files on first run (`briefing-config.json`, `ha-config.json`, `reminders-config.json`, `todo.txt`) next to the built executable (or under `RECEIPTPRINTER_CONFIG_DIR`, if set). These contain location coordinates, Home Assistant tokens, and Apple app-specific passwords - **they are git-ignored and must never be committed.**

### Config files

- `briefing-config.json` - latitude/longitude/location name for the weather lookup (Open-Meteo, no API key needed).
- `ha-config.json` - Home Assistant base URL, a long-lived access token, the entity/attribute the to-do list lives in, and (optionally) the entity IDs feeding your Energy dashboard for solar production, grid import/export, and gas.
- `reminders-config.json` - Apple ID + app-specific password + list name, for the (mostly unused) direct CalDAV fallback.
- `todo.txt` - plain-text fallback to-do list, used only if Home Assistant isn't configured.

## `ReceiptPrinter.Service`

The HTTP API + scheduler: endpoints work and it's been run against the real printer, but it's still light on production hardening (no API auth, no reconnect logic if the printer connection drops). Configured via `appsettings.json` (`Printer:Type`/`Port`/`Baud`/`NetworkHost`, `Briefing:ScheduledHour`/`Minute`); as a Home Assistant add-on, its options map onto the same settings instead (see below).

- `POST /print` - accepts a `Receipt` as JSON and prints it directly.
- `POST /briefing/trigger` - builds and prints the daily briefing on demand.
- `POST /todos/check` - the to-do note checker: compares the current to-do list (same source as the briefing's `TodoWidget`) against a small persisted store (`todo-note-store.json`) of what's already been printed. Anything new gets its own little note printed - a `TODO` heading, the item text, and the date (`dd-MM-yyyy`) it was printed. Anything that's dropped out of the source (presumably finished and thrown away) is just forgotten, no reprint.
- A background hosted service (`BriefingScheduler`) prints the daily briefing automatically once a day at the configured time.

Run it (from `src/ReceiptPrinter.Service`) with `dotnet run`. By default it prints with `Printer:Type=network`, i.e. it expects `ReceiptPrinter.NetworkSerialService` to be reachable at `Printer:NetworkHost`.

## Running as a Home Assistant add-on

The Service doesn't need to run on the same machine as the printer - it just needs network access to `ReceiptPrinter.NetworkSerialService`:

```
Home Assistant (add-on)                    PC (has the printer wired up over serial)
ReceiptPrinter.Service      --HTTP-->      ReceiptPrinter.NetworkSerialService --serial--> Woosim printer
(scheduler, HA polling,                    (forwards Receipt JSON straight to
 TODO-note checker)                         SerialWoosimPrinter)
```

- On the PC: `cd src/ReceiptPrinter.NetworkSerialService && dotnet run` - listens on `http://0.0.0.0:5251` by default (see its `appsettings.json` for the serial port/baud).
- In Home Assistant: **Settings -> Add-ons -> Add-on Store -> ⋮ -> Repositories**, add `https://github.com/ikkentim/woosim-printer`, then install "Receipt Printer Service" from the store. Configure `printer_network_host` (the PC's `host:port` from above) and the daily briefing time (`scheduled_hour`/`scheduled_minute`), then start it.

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
