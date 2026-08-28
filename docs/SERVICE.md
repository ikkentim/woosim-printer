# ReceiptPrinter.Service

A plain background worker - no HTTP server at all, every action is triggered over MQTT (see [MQTT entities](MQTT.md)). It's been run against the real printer, but is still light on production hardening (no reconnect logic if the printer connection drops).

## How it works

- `MqttAddonService` (hosted service) subscribes to the MQTT command topics backing the entities in [MQTT entities](MQTT.md) and does the actual printing.
- **The to-do note checker**: compares the current to-do list (same source as the briefing's `TodoWidget`) against a small persisted store (`todo-note-store.json`) of what's already been printed. Anything new gets its own little note printed - a `TODO` heading, the item text, and the date (`dd-MM-yyyy`) it was printed. Anything that's dropped out of the source (presumably finished and thrown away) is just forgotten, no reprint. A no-op if `Briefing:TodoNotesEnabled` is `false`.
- On startup, logs whether Home Assistant connectivity resolved (and from where), without ever exposing the token value - the first thing to check in the add-on's log if the briefing comes back with sections missing.

## Running the Service

```bash
cd src/ReceiptPrinter.Service
dotnet run
```

By default it prints with `Printer:Type=network`, i.e. it expects `ReceiptPrinter.NetworkSerialService` to be reachable at `Printer:NetworkHost`.

See [Configuration](CONFIGURATION.md) for all configuration options.
