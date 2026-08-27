# Woosim Receipt Printer

Reviving an old Woosim serial thermal receipt printer (salvaged from a photobooth) and hooking it up to a daily briefing / to-do printout, with a longer-term goal of running it standalone on an ESP32 over WiFi, driven by Home Assistant.

## Status

- [x] Printer talks over RS-232 (COM3, 9600 baud) from a Windows PC via a USB-to-serial adapter.
- [x] Receipts are built as plain data (a `Receipt` of `IElement`s) and handed to an `IReceiptPrinter` - callers never touch ESC/POS commands or connection state directly.
- [x] Daily briefing printout (in Dutch): date, weather, calendar (today + upcoming), a to-do list, and yesterday's energy usage (solar/grid/gas) - each section is a self-contained "widget" behind `IBriefingWidget`, producing elements rather than writing to a printer.
- [x] To-do list sourced from Apple Reminders, via an iOS Shortcut pushing to a Home Assistant webhook.
- [x] Calendar events sourced from Home Assistant's `caldav` integration (iCloud calendar).
- [x] Energy usage (solar production, grid import/export, gas) pulled straight from Home Assistant's long-term statistics (the same data behind the Energy dashboard), via its WebSocket API.
- [x] A background service scaffold (`ReceiptPrinter.Service`) exposing an HTTP API, a scheduled daily briefing, and a "new to-do -> print its own note" checker - see [Service](#receiptprinterservice) below.
- [ ] Move the printer off the PC onto a standalone ESP32 (in progress - parts ordered). `NetworkWoosimPrinter` is a stub waiting on the ESP32 firmware; see [docs/HARDWARE.md](docs/HARDWARE.md).

## Project layout

A multi-project solution ([`src/ReceiptPrinter.sln`](src/ReceiptPrinter.sln)) split by concern, so callers (the CLI, the Service) don't need to know printer-specific details, and any printer transport can plug in behind the same contract:

- **[`ReceiptPrinter.Contracts`](src/ReceiptPrinter.Contracts)** - shared library referenced by everything else. Contains:
  - [`IElement.cs`](src/ReceiptPrinter.Contracts/IElement.cs) / [`TextElement.cs`](src/ReceiptPrinter.Contracts/TextElement.cs) / [`Receipt.cs`](src/ReceiptPrinter.Contracts/Receipt.cs) / [`CutStyle.cs`](src/ReceiptPrinter.Contracts/CutStyle.cs) / [`Justification.cs`](src/ReceiptPrinter.Contracts/Justification.cs) - the data model for a receipt. A `TextElement` fully describes its own formatting (bold, size, justification, underline), so nothing needs to track "current printer state" between elements. `IElement` is JSON-polymorphic (see `[JsonDerivedType]` on it) so a `Receipt` can round-trip through the Service's HTTP API.
  - [`IReceiptPrinter.cs`](src/ReceiptPrinter.Contracts/IReceiptPrinter.cs) - `Task PrintAsync(Receipt receipt)`. That's the whole contract - each implementation manages opening/closing its own connection internally.
  - [`IBriefingWidget.cs`](src/ReceiptPrinter.Contracts/IBriefingWidget.cs) - a widget fetches its own data and returns the elements for its section (`DateHeaderWidget`, `WeatherWidget`, `CalendarWidget`, `TodoWidget`, `EnergyWidget`).
  - [`DailyBriefing.cs`](src/ReceiptPrinter.Contracts/DailyBriefing.cs) - runs the widgets and assembles the full `Receipt`.
  - [`BriefingConfig.cs`](src/ReceiptPrinter.Contracts/BriefingConfig.cs) - shared config loading (location, Home Assistant, Apple Reminders, to-do file).
  - [`HomeAssistantTodos.cs`](src/ReceiptPrinter.Contracts/HomeAssistantTodos.cs) / [`HomeAssistantCalendar.cs`](src/ReceiptPrinter.Contracts/HomeAssistantCalendar.cs) / [`HomeAssistantEnergy.cs`](src/ReceiptPrinter.Contracts/HomeAssistantEnergy.cs) - pull data from Home Assistant (REST for todos/calendar, WebSocket for long-term energy statistics).
  - [`AppleReminders.cs`](src/ReceiptPrinter.Contracts/AppleReminders.cs) - a CalDAV client for iCloud Reminders (kept as a fallback/reference; see [docs/HARDWARE.md](docs/HARDWARE.md#notes--gotchas) for why it doesn't see most real reminders lists).
- **[`ReceiptPrinter.Serial`](src/ReceiptPrinter.Serial)** - `SerialWoosimPrinter`, the ESC/POS driver actually in use today, translating `Receipt` elements into bytes over a serial port.
- **[`ReceiptPrinter.Network`](src/ReceiptPrinter.Network)** - `NetworkWoosimPrinter`, **TODO, not implemented** - will drive a printer connected to a standalone ESP32 over WiFi/HTTP.
- **[`ReceiptPrinter.CLI`](src/ReceiptPrinter.CLI)** - the console app for manual use: `test`/`briefing`/`reminders-debug` commands.
- **[`ReceiptPrinter.Service`](src/ReceiptPrinter.Service)** - a scaffold, see below.
- `docs/HARDWARE.md` - the hardware plan for moving the printer onto an ESP32.
- `ref/` - vendor manuals, old SDKs, and a ~10-year-old C# project for this same printer (git-ignored, kept locally only).

## Running the CLI

```bash
cd src/ReceiptPrinter.CLI

# dotnet run -- <command> [printer-type] [printer-args...]
dotnet run -- test                          # basic ESC/POS test print, serial/COM3/9600 by default
dotnet run -- test serial COM3 9600         # same, explicit
dotnet run -- test network printer.local    # TODO: not implemented yet - throws until the ESP32 firmware exists

dotnet run -- briefing                      # the daily briefing, serial by default
dotnet run -- reminders-debug               # lists Apple Reminders CalDAV lists + contents, for debugging
```

The `briefing` command auto-generates local config files on first run (`briefing-config.json`, `ha-config.json`, `reminders-config.json`, `todo.txt`) next to the built executable. These contain location coordinates, Home Assistant tokens, and Apple app-specific passwords - **they are git-ignored and must never be committed.**

### Config files

- `briefing-config.json` - latitude/longitude/location name for the weather lookup (Open-Meteo, no API key needed).
- `ha-config.json` - Home Assistant base URL, a long-lived access token, the entity/attribute the to-do list lives in, and (optionally) the entity IDs feeding your Energy dashboard for solar production, grid import/export, and gas.
- `reminders-config.json` - Apple ID + app-specific password + list name, for the (mostly unused) direct CalDAV fallback.
- `todo.txt` - plain-text fallback to-do list, used only if Home Assistant isn't configured.

## `ReceiptPrinter.Service`

A **TODO scaffold** - it compiles, its endpoints work, but it hasn't been hardened (no API auth, no reconnect logic if the printer drops) or run long-term. Configured via `appsettings.json` (`Printer:Type`/`Port`/`Baud`/`NetworkHost`, `Briefing:ScheduledHour`/`Minute`). Needs the same `ha-config.json` etc. as the CLI, placed next to its build output.

- `POST /print` - accepts a `Receipt` as JSON and prints it directly.
- `POST /briefing/trigger` - builds and prints the daily briefing on demand.
- `POST /todos/check` - the to-do note checker: compares the current to-do list (same source as the briefing's `TodoWidget`) against a small persisted store (`todo-note-store.json`) of what's already been printed. Anything new gets its own little note printed - a `TODO` heading, the item text, and the date (`dd-MM-yyyy`) it was printed. Anything that's dropped out of the source (presumably finished and thrown away) is just forgotten, no reprint.
- A background hosted service (`BriefingScheduler`) prints the daily briefing automatically once a day at the configured time.

Run it (from `src/ReceiptPrinter.Service`) with `dotnet run`.

## To-do list data flow

Apple Reminders lists created after Reminders' CloudKit-based redesign don't get exposed over CalDAV, so pulling them server-side isn't possible. Instead:

```
iPhone (Shortcuts: "Find Reminders" -> Combine Text -> Get Contents of URL)
   -> Home Assistant webhook
   -> trigger-based template sensor (sensor.todo_list, "items" attribute - no length cap)
   -> this app polls the HA REST API when printing
```

See [docs/HARDWARE.md](docs/HARDWARE.md#notes--gotchas) for the full story of why this ended up being necessary.
