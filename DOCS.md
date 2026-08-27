# Receipt Printer Service

Runs a daily-briefing / to-do printer for a Woosim receipt printer inside Home Assistant, triggered from
your own automations over MQTT (no HTTP API, no internal schedule). See the
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
2. A broker set up in Home Assistant (e.g. the official Mosquitto add-on) - **required**, since MQTT is
   the only way to trigger this add-on at all.
3. Install this add-on and open its **Configuration** tab. Everything is grouped to match the app's
   settings directly:
   - `Printer.NetworkHost` - that PC's `host:port` (e.g. `192.168.1.50:5251`).
   - `HomeAssistant` - `TodoEntityId` etc. for the to-do list and Energy dashboard entities, per the main
     README. There's nothing else to fill in here: this add-on already has `homeassistant_api: true`, so
     it talks to Home Assistant through Supervisor's own proxy with a scoped token automatically, and
     reads Home Assistant's own configured latitude/longitude for the weather widget - no personal
     long-lived access token and no location to enter.
   - `Briefing` - language, and which widgets run and in what order - see
     [Configuration](https://github.com/ikkentim/woosim-printer#configuration) in the main README.
4. Start the add-on, then wire up a Home Assistant automation using the MQTT entities below to trigger
   the briefing/to-do check on whatever schedule you want - nothing runs automatically on its own. Config
   changes in the Configuration tab apply live - no restart needed.

## Home Assistant entities (MQTT)

On startup, this add-on publishes MQTT discovery configs so no YAML is needed - everything shows up under
one "Receipt Printer Service" device:

- **button.receipt_printer_print_daily_briefing** - builds and prints the daily briefing.
- **button.receipt_printer_check_to_dos_now** - runs the to-do note checker (a no-op if
  `Briefing.TodoNotesEnabled` is off).
- **notify.receipt_printer_print** - `notify.send_message` prints the message. Supports a tiny markdown
  dialect entirely in the message text: a `~~~` line requests a full cut instead of the default partial
  one; a `[WidgetName]` line (e.g. `[Weather]`, `[DailyBriefing]`) splices in that widget's live output;
  `>`/`>>` at the start of a line center/right-justify it; `# heading` for a big bold line;
  `**bold**`/`*underline*` inline (mixable/nestable); `\*`/`\#`/`\>`/`\~`/`\\` to escape a literal
  character.
- **binary_sensor.receipt_printer_printer_reachable** - `on`/`off`, checked every minute without
  printing anything (network: hits the network service's `/health`; serial: checks the COM port is
  still enumerated).

There's also a startup log line reporting whether Home Assistant connectivity resolved (and from where),
without ever exposing the token value - check the add-on's log first if the briefing comes back with
sections missing.

If no MQTT broker is configured in Home Assistant at all, the add-on logs this and idles - there's no
other way to trigger it.
