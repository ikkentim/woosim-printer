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
2. Install this add-on and set `printer_network_host` to that PC's `host:port` (e.g.
   `192.168.1.50:5251`), and the daily briefing time (`scheduled_hour`/`scheduled_minute`).
3. Start the add-on. Its `/data` folder (Settings -> Add-ons -> Receipt Printer Service -> "Show disk
   usage" / accessible via the Samba or SSH add-on) is where `ha-config.json`, `reminders-config.json`,
   `briefing-config.json`, `todo.txt` and `todo-note-store.json` live - fill those in there, using the
   same fields documented in the main README. They never need to go in this repo.

## Endpoints

- `POST /print` - accepts a `Receipt` as JSON and prints it directly.
- `POST /briefing/trigger` - builds and prints the daily briefing on demand.
- `POST /todos/check` - checks the to-do list for new items and prints a note for each one.

A background task also prints the daily briefing automatically at `scheduled_hour`:`scheduled_minute`
every day.
