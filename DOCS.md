# Receipt Printer Service

Runs the daily-briefing / to-do scheduler for a Woosim receipt printer inside Home Assistant. See the
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
   - `Location` - coordinates for the weather widget.
   - `HomeAssistant` - leave `BaseUrl`/`Token` **empty** to talk to Home Assistant through Supervisor's
     own proxy (this add-on already has `homeassistant_api: true`, so it gets a scoped token
     automatically - no personal long-lived access token needed). Fill in `TodoEntityId` etc. for the
     to-do list and Energy dashboard entities, per the main README.
   - `Briefing` - language, which widgets run and in what order, and the to-do-note/schedule toggles -
     see [Configuration](https://github.com/ikkentim/woosim-printer#configuration) in the main README.
3. Start the add-on. Config changes in this tab apply live - no restart needed.

## Endpoints

- `POST /print` - accepts a `Receipt` as JSON and prints it directly.
- `POST /briefing/trigger` - builds and prints the daily briefing on demand.
- `POST /todos/check` - checks the to-do list for new items and prints a note for each one (a no-op if
  `Briefing.TodoNotesEnabled` is off).

A background task also prints the daily briefing automatically per `Briefing.ScheduledBriefingEnabled`/
`ScheduledHour`/`ScheduledMinute` (default: enabled, 07:00).
