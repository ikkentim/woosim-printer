# Receipt Printer Service (Home Assistant add-on)

Packages `ReceiptPrinter.Service` (the scheduler + HTTP API - see [the main README](../../README.md#receiptprinterservice))
to run inside Home Assistant as an add-on, instead of on the PC that has the printer wired up.

It prints by talking over the network to `ReceiptPrinter.NetworkSerialService` (see
[src/ReceiptPrinter.NetworkSerialService](../../src/ReceiptPrinter.NetworkSerialService)), which stays
running on the PC next to the actual serial-attached Woosim printer - that's the piece standing in for
the ESP32 firmware that doesn't exist yet (see [docs/HARDWARE.md](../../docs/HARDWARE.md)).

```
Home Assistant add-on (this)              PC (wherever the printer is plugged in)
ReceiptPrinter.Service      --HTTP-->     ReceiptPrinter.NetworkSerialService --serial--> Woosim printer
(scheduler, HA polling,                   (just forwards Receipt JSON to
 TODO-note checker)                        SerialWoosimPrinter)
```

## Why a prebuilt image

Home Assistant Supervisor builds local add-ons using the add-on's own folder as the Docker build
context, but `ReceiptPrinter.Service` needs its sibling projects under `src/` too (`Contracts`,
`Serial`, `Network`). So this add-on ships as a **prebuilt image** rather than building in place:

```bash
# from the repo root
docker build -f ha-addon/receiptprinter-service/Dockerfile -t <your-registry>/receiptprinter-service:latest .
docker push <your-registry>/receiptprinter-service:latest
```

Then edit `config.yaml`'s `image:` to point at that tag (or run this repo's build via CI/GHCR and never
build it locally at all).

## Installing

1. Build and push the image (above), or set `image:` to wherever you host it.
2. Add this repository (the parent of `ha-addon/`) as a Home Assistant add-on repository, or copy the
   `ha-addon/receiptprinter-service` folder into `/addons/` on the HA host as a local add-on.
3. Install + start it from Settings -> Add-ons. Set `printer_network_host` to the PC's
   `host:port` running `ReceiptPrinter.NetworkSerialService` (default port `5251`), and the daily
   briefing time (`scheduled_hour`/`scheduled_minute`).
4. The add-on's persistent `/data` folder (exposed to the add-on config filesystem) is where
   `ha-config.json`, `reminders-config.json`, `briefing-config.json`, `todo.txt` and
   `todo-note-store.json` live - use the Studio Code Server / Samba / SSH add-on to edit them in, same
   fields as documented in [the main README](../../README.md#config-files). They're git-ignored on
   purpose; nothing there ever needs to go in this repo.

## On the PC with the printer

Run `ReceiptPrinter.NetworkSerialService` there (it needs direct access to the COM port, so it can't run
inside the HA add-on's container):

```bash
cd src/ReceiptPrinter.NetworkSerialService
dotnet run
```

It listens on `http://0.0.0.0:5251` by default (see its `appsettings.json` for the serial port/baud) and
exposes `POST /print` - the same tiny contract `NetworkWoosimPrinter` speaks, whether the caller is this
add-on or the CLI's `network` printer type.
