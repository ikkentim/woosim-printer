# Woosim Receipt Printer

Reviving an old Woosim serial thermal receipt printer (salvaged from a photobooth) and hooking it up to a daily briefing / to-do printout, with a longer-term goal of running it standalone on an ESP32 over WiFi, driven by Home Assistant.

## Status

- [x] Printer talks over RS-232 (COM3, 9600 baud) from a Windows PC via a USB-to-serial adapter.
- [x] C# console app drives it with ESC/POS commands (text, bold, sizing, justification, cut).
- [x] Daily briefing printout: date, weather, calendar (today + upcoming), and a to-do list.
- [x] To-do list sourced from Apple Reminders, via an iOS Shortcut pushing to a Home Assistant webhook.
- [x] Calendar events sourced from Home Assistant's `caldav` integration (iCloud calendar).
- [ ] Move the printer off the PC onto a standalone ESP32 (in progress - parts ordered).
- [ ] Home Assistant automations that print receipts directly (doorbell log, notifications, etc).

## Project layout

- [`src/ReceiptPrinter`](src/ReceiptPrinter) - the C# console app.
  - [`WoosimPrinter.cs`](src/ReceiptPrinter/WoosimPrinter.cs) - minimal ESC/POS driver over a serial port.
  - [`DailyBriefing.cs`](src/ReceiptPrinter/DailyBriefing.cs) - builds and prints the daily briefing receipt.
  - [`HomeAssistantTodos.cs`](src/ReceiptPrinter/HomeAssistantTodos.cs) / [`HomeAssistantCalendar.cs`](src/ReceiptPrinter/HomeAssistantCalendar.cs) - pull data from a Home Assistant instance's REST API.
  - [`AppleReminders.cs`](src/ReceiptPrinter/AppleReminders.cs) - a CalDAV client for iCloud Reminders (kept as a fallback/reference; see [docs/HARDWARE.md](docs/HARDWARE.md#notes--gotchas) for why it doesn't see most real reminders lists).
- `docs/HARDWARE.md` - the hardware plan for moving the printer onto an ESP32.
- `ref/` - vendor manuals, old SDKs, and a ~10-year-old C# project for this same printer (git-ignored, kept locally only).

## Running it

```bash
cd src/ReceiptPrinter
dotnet run -- test COM3 9600        # basic ESC/POS test print
dotnet run -- briefing COM3 9600    # the daily briefing
dotnet run -- reminders-debug       # lists Apple Reminders CalDAV lists + contents, for debugging
```

The `briefing` command auto-generates local config files on first run (`briefing-config.json`, `ha-config.json`, `reminders-config.json`, `todo.txt`) next to the built executable. These contain location coordinates, Home Assistant tokens, and Apple app-specific passwords - **they are git-ignored and must never be committed.**

### Config files

- `briefing-config.json` - latitude/longitude/location name for the weather lookup (Open-Meteo, no API key needed).
- `ha-config.json` - Home Assistant base URL, a long-lived access token, and the entity/attribute the to-do list lives in.
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
