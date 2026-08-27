# Receipt Printer Service

Runs a daily-briefing / to-do printer for a Woosim receipt printer inside Home Assistant, triggered from
your own automations (HTTP or MQTT) rather than an internal schedule. See the
[project README](https://github.com/ikkentim/woosim-printer) for the full picture (hardware, to-do data
flow, etc.) - this covers just the add-on.

## How it fits together

The printer itself is still wired up over serial to a PC, not to Home Assistant. This add-on doesn't talk
to the printer directly - it prints by sending an HTTP request to `ReceiptPrinter.NetworkSerialService`
(a small program you run on that PC), which forwards it to the printer over serial.

```
Home Assistant (this add-on)                PC (has the printer wired up over serial)
ReceiptPrinter.Service      --HTTP-->       ReceiptPrinter.NetworkSerialService --serial--> Woosim printer
```

## Setup

1. On the PC with the printer: `cd src/ReceiptPrinter.NetworkSerialService && dotnet run` (see the main
   repo). It listens on port `5251` by default.
2. Install this add-on and open its **Configuration** tab. Everything is grouped to match the app's
   settings directly:
   - `Printer.NetworkHost` - that PC's `host:port` (e.g. `192.168.1.50:5251`).
   - `HomeAssistant` - `TodoEntityId` etc. for the to-do list and Energy dashboard entities, per the main
     README. There's nothing else to fill in here: this add-on already has `homeassistant_api: true`, so
     it talks to Home Assistant through Supervisor's own proxy with a scoped token automatically, and
     reads Home Assistant's own configured latitude/longitude for the weather widget - no personal
     long-lived access token and no location to enter.
   - `Briefing` - language, and which widgets run and in what order - see
     [Configuration](https://github.com/ikkentim/woosim-printer#configuration) in the main README.
   - `Mqtt` - turn discovery entities on/off, see below.
3. Start the add-on, then wire up a Home Assistant automation (HTTP `rest_command` or the MQTT buttons
   below) to trigger the briefing/to-do check on whatever schedule you want - nothing runs automatically
   on its own. Config changes in the Configuration tab apply live - no restart needed.

## Endpoints

- `POST /print` - accepts a `Receipt` as JSON and prints it directly.
- `POST /briefing/trigger` - builds and prints the daily briefing on demand.
- `POST /todos/check` - checks the to-do list for new items and prints a note for each one (a no-op if
  `Briefing.TodoNotesEnabled` is off).
- `GET /diag/home-assistant` - reports whether Home Assistant connectivity resolved, without ever
  exposing the token value. Useful when the briefing comes back with sections missing.

Nothing runs on an internal schedule - trigger these from a Home Assistant automation.

## Home Assistant entities (MQTT)

If a broker is set up in Home Assistant (e.g. the official Mosquitto add-on), this add-on publishes
MQTT discovery configs on startup, so no YAML is needed - everything shows up under one "Receipt Printer
Service" device:

- **button.receipt_printer_print_daily_briefing** - same as `POST /briefing/trigger`.
- **button.receipt_printer_check_to_dos_now** - same as `POST /todos/check`.
- **notify.receipt_printer_print** - `notify.send_message` with any text prints it as a plain receipt.
- **binary_sensor.receipt_printer_printer_reachable** - `on`/`off`, checked every minute without
  printing anything (network: hits the network service's `/health`; serial: checks the COM port is
  still enumerated).

Turn this off with `Mqtt.Enabled: false` if you'd rather not use it - the HTTP endpoints above always
work regardless.
