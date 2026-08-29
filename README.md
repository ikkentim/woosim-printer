# Woosim Receipt Printer

Reviving an old Woosim serial thermal receipt printer (salvaged from a photobooth) and hooking it up to a daily briefing / to-do printout - date, weather, calendar, to-do list, and yesterday's energy usage - driven by Home Assistant, with a longer-term goal of running the printer standalone on an ESP32 over WiFi instead of a PC.

This repository doubles as a Home Assistant **add-on repository**.

## Status

- [x] Printer talks over RS-232 (COM3, 9600 baud) via a USB-to-serial adapter.
- [x] Receipts are built as plain data (a `Receipt` of `IElement`s) and handed to an `IReceiptPrinter` - callers never touch ESC/POS commands or connection state directly.
- [x] Daily briefing printout: date, weather, calendar, to-do list, energy usage - each section is a self-contained "widget" behind `IBriefingWidget`. Language (Dutch/English) and widget order are configurable.
- [x] To-do list sourced from Apple Reminders via an iOS Shortcut pushing to a Home Assistant webhook.
- [x] Calendar events sourced from Home Assistant's `caldav` integration (iCloud calendar).
- [x] Energy usage pulled from Home Assistant's long-term statistics via its WebSocket API.
- [x] `ReceiptPrinter.Service` - MQTT-triggered background service with a "new to-do → print its own note" checker, runs anywhere on the network and prints via `ReceiptPrinter.NetworkSerialService` on the PC.
- [x] Move the printer off the PC onto a standalone ESP32 - [`firmware/`](firmware/) (D1 Mini ESP32, HTTP -> UART bridge) verified end to end. See [Hardware plan](docs/HARDWARE.md).

## Quick start

**Choose your path:**

- 🏠 **Home Assistant**: [Install the add-on](docs/SETUP-ADDON.md)
- 🖥️ **Manual/CLI**: [Run the CLI tool](docs/RUNNING-CLI.md)
- 📁 **Documentation**: [View all docs](docs/)

## Project layout

A multi-project solution ([`src/ReceiptPrinter.slnx`](src/ReceiptPrinter.slnx)) split by concern:

- **[`ReceiptPrinter.Shared`](src/ReceiptPrinter.Shared)** - receipt data model, the `Receipt` -> ESC/POS encoder, widget contracts, Home Assistant integration, configuration
- **[`ReceiptPrinter.Serial`](src/ReceiptPrinter.Serial)** - drives the Woosim printer over serial (opens the port, writes the encoded bytes)
- **[`ReceiptPrinter.Network`](src/ReceiptPrinter.Network)** - network transport (POSTs the encoded ESC/POS bytes over HTTP)
- **[`ReceiptPrinter.NetworkSerialService`](src/ReceiptPrinter.NetworkSerialService)** - dumb HTTP-to-serial passthrough that runs next to the printer; C# reference for the firmware contract, still usable as a PC-side stand-in
- **[`ReceiptPrinter.CLI`](src/ReceiptPrinter.CLI)** - console app for manual use
- **[`ReceiptPrinter.Service`](src/ReceiptPrinter.Service)** - MQTT-triggered background service

Plus **[`firmware/`](firmware/)** (not part of the .NET solution) - the ESP32 firmware that replaces `NetworkSerialService` on real hardware; same `POST /print` / `GET /health` contract, streamed straight to the UART. See [`firmware/README.md`](firmware/README.md).

See [docs/](docs/) for detailed architecture and feature documentation.

## Documentation

The [docs/](docs/) folder contains detailed guides organized by topic:

| Document | Topic |
|---|---|
| [SETUP-ADDON.md](docs/SETUP-ADDON.md) | Installing and configuring the Home Assistant add-on |
| [RUNNING-CLI.md](docs/RUNNING-CLI.md) | Running the CLI tool |
| [CONFIGURATION.md](docs/CONFIGURATION.md) | All configuration options (Printer, Home Assistant, Briefing) |
| [SERVICE.md](docs/SERVICE.md) | Background service details and to-do note checker |
| [MQTT.md](docs/MQTT.md) | MQTT entities and custom print formatting (ReceiptMarkdown) |
| [TODO-DATA-FLOW.md](docs/TODO-DATA-FLOW.md) | Why CalDAV doesn't work and how the to-do webhook is set up |
| [HARDWARE.md](docs/HARDWARE.md) | ESP32 migration plan, parts list, wiring diagram, safety notes, flashing the firmware |

## License

[MIT](LICENSE)

Bundled weather icons are [Material Design Icons](https://pictogrammers.com/library/mdi/) under the
[Pictogrammers Free License](https://pictogrammers.com/docs/general/license/) - see
[`src/ReceiptPrinter.Shared/Assets/Weather/README.md`](src/ReceiptPrinter.Shared/Assets/Weather/README.md).
