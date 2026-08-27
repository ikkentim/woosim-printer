# Woosim Receipt Printer

Reviving an old Woosim serial thermal receipt printer (salvaged from a photobooth) and hooking it up to a daily briefing / to-do printout, with a longer-term goal of running it standalone on an ESP32 over WiFi, driven by Home Assistant.

## Status

- [x] Printer talks over RS-232 (COM3, 9600 baud) from a Windows PC via a USB-to-serial adapter.
- [x] C# console app drives it with ESC/POS commands (text, bold, sizing, justification, cut), behind an `IReceiptPrinter` abstraction so the transport is swappable.
- [x] Daily briefing printout (in Dutch): date, weather, calendar (today + upcoming), a to-do list, and yesterday's energy usage (solar/grid/gas) - each section is a self-contained "widget" behind `IBriefingWidget`.
- [x] To-do list sourced from Apple Reminders, via an iOS Shortcut pushing to a Home Assistant webhook.
- [x] Calendar events sourced from Home Assistant's `caldav` integration (iCloud calendar).
- [x] Energy usage (solar production, grid import/export, gas) pulled straight from Home Assistant's long-term statistics (the same data behind the Energy dashboard), via its WebSocket API.
- [ ] Move the printer off the PC onto a standalone ESP32 (in progress - parts ordered). `NetworkWoosimPrinter` is a stub waiting on the ESP32 firmware; see [docs/HARDWARE.md](docs/HARDWARE.md).
- [ ] Home Assistant automations that print receipts directly (doorbell log, notifications, etc).

## Project layout

- [`src/ReceiptPrinter`](src/ReceiptPrinter) - the C# console app.
  - [`IReceiptPrinter.cs`](src/ReceiptPrinter/IReceiptPrinter.cs) - the printer abstraction (`Justification`/`CutMode` enums + interface) that the rest of the app codes against.
  - [`SerialWoosimPrinter.cs`](src/ReceiptPrinter/SerialWoosimPrinter.cs) - ESC/POS driver over a serial port (the one actually in use today).
  - [`NetworkWoosimPrinter.cs`](src/ReceiptPrinter/NetworkWoosimPrinter.cs) - **TODO, not implemented** - will drive a printer connected to a standalone ESP32 over WiFi/HTTP.
  - [`DailyBriefing.cs`](src/ReceiptPrinter/DailyBriefing.cs) - orchestrator: runs each widget in order, then feeds/cuts.
  - [`IBriefingWidget.cs`](src/ReceiptPrinter/IBriefingWidget.cs) - the widget interface; each section of the receipt (`DateHeaderWidget`, `WeatherWidget`, `CalendarWidget`, `TodoWidget`, `EnergyWidget`) implements it independently, fetching its own data and rendering itself.
  - [`BriefingConfig.cs`](src/ReceiptPrinter/BriefingConfig.cs) - shared config loading (location, Home Assistant, Apple Reminders, to-do file) used by the widgets above.
  - [`HomeAssistantTodos.cs`](src/ReceiptPrinter/HomeAssistantTodos.cs) / [`HomeAssistantCalendar.cs`](src/ReceiptPrinter/HomeAssistantCalendar.cs) / [`HomeAssistantEnergy.cs`](src/ReceiptPrinter/HomeAssistantEnergy.cs) - pull data from a Home Assistant instance (REST API for todos/calendar, WebSocket API for long-term energy statistics).
  - [`AppleReminders.cs`](src/ReceiptPrinter/AppleReminders.cs) - a CalDAV client for iCloud Reminders (kept as a fallback/reference; see [docs/HARDWARE.md](docs/HARDWARE.md#notes--gotchas) for why it doesn't see most real reminders lists).
- `docs/HARDWARE.md` - the hardware plan for moving the printer onto an ESP32.
- `ref/` - vendor manuals, old SDKs, and a ~10-year-old C# project for this same printer (git-ignored, kept locally only).

## Running it

```bash
cd src/ReceiptPrinter

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

## To-do list data flow

Apple Reminders lists created after Reminders' CloudKit-based redesign don't get exposed over CalDAV, so pulling them server-side isn't possible. Instead:

```
iPhone (Shortcuts: "Find Reminders" -> Combine Text -> Get Contents of URL)
   -> Home Assistant webhook
   -> trigger-based template sensor (sensor.todo_list, "items" attribute - no length cap)
   -> this app polls the HA REST API when printing
```

See [docs/HARDWARE.md](docs/HARDWARE.md#notes--gotchas) for the full story of why this ended up being necessary.
