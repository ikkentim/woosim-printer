# Running as a Home Assistant add-on

The Service doesn't need to run on the same machine as the printer - it just needs network access to `ReceiptPrinter.NetworkSerialService`:

```
Home Assistant (add-on)                    PC (has the printer wired up over serial)
ReceiptPrinter.Service      --HTTP-->      ReceiptPrinter.NetworkSerialService --serial--> Woosim printer
(MQTT-triggered,                           (copies the POSTed ESC/POS bytes
 HA polling, TODO-note checker,             straight to the serial port)
 Receipt -> ESC/POS encoding)
```

## Setup steps

### 1. On the PC with the printer

```bash
cd src/ReceiptPrinter.NetworkSerialService
dotnet run
```

It listens on `http://0.0.0.0:5251` by default (see its `appsettings.json` for the serial port/baud).

### 2. Set up MQTT broker in Home Assistant

A broker (e.g. the official Mosquitto add-on) is required, since MQTT is the only way to trigger this add-on at all.

### 3. Install the add-on

In Home Assistant:
- **Settings -> Add-ons -> Add-on Store -> ⋮ -> Repositories**
- Add `https://github.com/ikkentim/woosim-printer`
- Find "Receipt Printer Service" in the store and install it

### 4. Configure the add-on

Open the add-on's **Configuration** tab. Everything is grouped to match the app's settings directly:

- **`Printer.NetworkHost`** - the PC's `host:port` (e.g. `192.168.1.50:5251`)
- **`HomeAssistant`** section:
  - `TodoEntityId` / `TodoAttributeName` for the to-do list
  - Entity IDs feeding your Energy dashboard (`SolarProductionEntityId`, `GridImportEntityIds`/`GridExportEntityIds`, `GasEntityId`)
  - **No `BaseUrl`/`Token` to fill in here**: this add-on has `homeassistant_api: true`, so it talks to Home Assistant through Supervisor's own proxy with a scoped token automatically, and reads Home Assistant's own configured latitude/longitude for the weather widget
- **`Briefing`** section:
  - `Language` - `Nl` or `En`
  - `Widgets` - which to run and in what order (see [Configuration](CONFIGURATION.md))

### 5. Start the add-on and wire up automations

Start the add-on, then wire up a Home Assistant automation using the MQTT entities (see [MQTT entities](MQTT.md)) to trigger the briefing/to-do check on whatever schedule you want - nothing runs automatically on its own.

Config changes in the Configuration tab apply live - no restart needed.

## How the add-on is built

The image is built and published to `ghcr.io/ikkentim/ha-{arch}-receiptprinter-service` by [`.github/workflows/builder.yaml`](.github/workflows/builder.yaml) on every push to `main`.

Supervisor just pulls the prebuilt image rather than building it on-device (this repo's Dockerfile needs the whole `src/` solution as build context, since `ReceiptPrinter.Service` references its sibling projects).

`config.yaml`, `Dockerfile`, `DOCS.md`, `CHANGELOG.md` and `repository.yaml` at the repo root are what make this a valid single-add-on repository - see the [Home Assistant add-on docs](https://developers.home-assistant.io/docs/add-ons) for the format.

## Wire protocol

`NetworkSerialService` speaks the same trivial wire protocol the real ESP32 firmware will, so it's a drop-in stand-in until the firmware exists:

- `POST /print` with an `application/octet-stream` body of raw ESC/POS bytes - the service copies the body straight to the serial port, byte for byte. All receipt formatting happens sender-side in [`EscPosEncoder`](../src/ReceiptPrinter.Shared/Printers/EscPosEncoder.cs); this side needs no knowledge of receipts, JSON, or ESC/POS.
- `GET /health` - returns `200 ok` without touching the printer (used for the "reachable" status).
