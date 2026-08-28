# Configuration

Everything (printer transport, Home Assistant, the briefing itself) is a single `ReceiptPrinterOptions` tree bound from `IConfiguration` (see [`ReceiptPrinterConfiguration.cs`](../src/ReceiptPrinter.Shared/Configuration/ReceiptPrinterConfiguration.cs)), layered the same way for the CLI and the Service:

## Configuration sources (in order of priority)

1. `appsettings.json` next to the executable - committed, safe non-secret defaults (widget order, etc.).
2. `appsettings.local.json` next to it (or under `RECEIPTPRINTER_CONFIG_DIR`, if set) - **git-ignored**, for your actual Home Assistant token when running outside Home Assistant.
3. `/data/options.json` - only present inside the Home Assistant add-on, written by Supervisor from its Configuration tab; reloads live, no restart needed.
4. Environment variables (e.g. `HomeAssistant__Token=...`, double-underscore for nesting) - highest priority, useful for CI/containers.

## Configuration sections

### Printer

- **`Type`** - `serial` or `network`
- **`Port`** - serial port name (e.g., `COM3` on Windows, `/dev/ttyUSB0` on Linux)
- **`Baud`** - serial baud rate (default: `9600`)
- **`NetworkHost`** - network service host:port (e.g., `ReceiptPrinter.NetworkSerialService`'s `host:port`)

### HomeAssistant

- **`BaseUrl`** - only needed when running outside Home Assistant (the add-on doesn't expose this)
- **`Token`** - only needed when running outside Home Assistant with a personal long-lived access token
- **`TodoEntityId`** - Home Assistant entity ID for the to-do list
- **`TodoAttributeName`** - attribute name on the to-do entity containing the list items
- **`SolarProductionEntityId`** - entity for solar production (energy widget)
- **`GridImportEntityIds`** - list of entities for grid import (summed for multi-tariff meters)
- **`GridExportEntityIds`** - list of entities for grid export (summed)
- **`GasEntityId`** - entity for gas usage

> **About Home Assistant add-on**: The add-on doesn't need `BaseUrl`/`Token` at all - it reaches Home Assistant through Supervisor's proxy using its own automatically-injected token. It also reads Home Assistant's own configured latitude/longitude for the weather widget, so there's no separate location setting.

### Briefing

- **`Language`** - `Nl` or `En` - translates every widget's labels via [`Localization.cs`](../src/ReceiptPrinter.Shared/Configuration/Localization.cs)
- **`Widgets`** - which to run and in what order (valid names: `DateHeader`, `Weather`, `Calendar`, `Todo`, `Energy`; empty/omitted defaults to all five)
- **`TodoNotesEnabled`** - whether the Service's to-do-note checker runs (false = disabled)

## File locations

- `todo.txt` - plain-text fallback to-do list, used only if Home Assistant isn't configured
- `todo-note-store.json` - the Service's persisted "already printed" tracking for the to-do note checker
- These stay as plain files next to the executable or in `RECEIPTPRINTER_CONFIG_DIR` if set - they're free-form content/runtime state, not settings
