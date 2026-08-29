# Receipt Printer Service

Runs a daily-briefing / to-do printer for a Woosim receipt printer inside Home Assistant, triggered from
your own automations over MQTT (no HTTP API, no internal schedule). See the
[project README](https://github.com/ikkentim/woosim-printer) for the full picture (hardware, to-do data
flow, etc.) - this covers just the add-on.

## How it fits together

This add-on doesn't talk to the printer directly - it builds the receipt, encodes it to raw ESC/POS
bytes, and POSTs those to a small HTTP endpoint next to the printer, which streams them to the wire.
That endpoint is normally the ESP32 firmware (see the main repo's `firmware/`); a PC running
`ReceiptPrinter.NetworkSerialService` with a USB-serial adapter works too.

```
Home Assistant (this add-on)             next to the printer
ReceiptPrinter.Service    --HTTP-->      ESP32 firmware --UART--> MAX3232 --RS-232--> Woosim printer
```

## Setup

1. Have the printer host running: flash/wire the ESP32 firmware (see the main repo), or on a PC with the
   printer `cd src/ReceiptPrinter.NetworkSerialService && dotnet run`. Either listens on port `5251`.
2. A broker set up in Home Assistant (e.g. the official Mosquitto add-on) - **required**, since MQTT is
   the only way to trigger this add-on at all.
3. Install this add-on and open its **Configuration** tab. Everything is grouped to match the app's
   settings directly:
   - `Printer.NetworkHost` - the printer host's `host:port` (e.g. `printer.local:5251` for the ESP32).
   - `HomeAssistant` - `TodoEntityId` etc. for the to-do list and Energy dashboard entities, per the main
     README. There's nothing else to fill in here: this add-on already has `homeassistant_api: true`, so
     it talks to Home Assistant through Supervisor's own proxy with a scoped token automatically - no
     personal long-lived access token to enter. The weather widget auto-discovers a `weather.*` entity
     (falling back to open-meteo with Home Assistant's own configured location), so there's nothing to
     set for it either.
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
  a `![name]` line (e.g. `![rainy]`) prints a weather glyph; `>`/`>>` at the start of a line
  center/right-justify it; `# heading` for a big bold line;
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
