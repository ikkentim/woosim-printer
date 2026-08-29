# Hardware plan: moving the printer to a standalone ESP32

> **Status:** firmware is written and lives in [`../firmware/`](../firmware/) (PlatformIO,
> LOLIN D1 Mini ESP32). Hardware is in hand; bring-up in progress. See
> [Flashing the firmware](#flashing-the-firmware) below.

## Background

The printer is an old Woosim serial thermal receipt printer, salvaged from a photobooth. It's currently wired as a bodge job: a single DE-9 (RS-232) cable carries both the data signals *and* injected DC power, since the photobooth build never had a separate power connector on the printer.

Current setup:
- Printer connected via RS-232 (DE-9), 9600 baud, to a PC over a USB-to-serial adapter.
- A 12V power supply is injected directly into the same DE-9 cable, downstream of the USB-serial adapter.

Goal: replace the PC + USB-serial adapter with a standalone ESP32, so the printer can be driven directly over WiFi/HTTP (e.g. from Home Assistant automations), with no PC involved.

## Firmware scope

The firmware stays deliberately dumb. All the receipt → ESC/POS translation happens on the sender
(`ReceiptPrinter.Network` / `EscPosEncoder`); the ESP32 only has to:

- serve `POST /print` - read the `application/octet-stream` request body and write it to the UART, byte for byte
- serve `GET /health` - return `200` without touching the printer

That's the whole contract. It's implemented twice: [`../firmware/src/main.cpp`](../firmware/src/main.cpp)
for the ESP32, and `ReceiptPrinter.NetworkSerialService` as a line-for-line C# reference that still
works as a PC-side stand-in.

## The existing cable's pinout

Measured directly from the photobooth's cable:

| Pin(s) | Function |
|---|---|
| 2 | RXD (signal) |
| 3 | TXD (signal) |
| 5 | GND (signal) |
| 6 + 7 (tied together) | +12V DC (power, injected) |
| 8 + 9 (tied together) | GND (power return) |

Only pins 2/3/5 are genuine RS-232 signal; 1/4 are unused. Pins 6-9 are a bodge to carry power over the same cable/connector rather than using a separate power jack.

## Why RS-232 needs a level shifter for the ESP32

RS-232 signal levels are bipolar, roughly ±5V to ±12V (spec allows ±3V to ±15V), with **negative** voltage as the logic "1"/mark - the opposite polarity convention from TTL. The ESP32's UART pins are 3.3V TTL. Connecting RS-232 signal lines directly to ESP32 GPIOs would very likely damage them.

A **MAX3232**-based level shifter sits in between and handles this conversion. It needs its own low-voltage supply (3.3-5.5V) - unrelated to the printer's separate 12V/2A power feed.

## Parts list

| Part | Purpose | Notes |
|---|---|---|
| [SP3232/MAX3232 RS232-to-UART module](https://www.tinytronics.nl/en/communication-and-signals/serial/rs-232/sp3232-rs232-to-uart-module-mount) | Level-shifts RS-232 <-> TTL | Has a female DE-9 socket on one side, a 6-pin header (VCC/GND/TXD/RXD/RTS/CTS) on the other. Runs on 3-5.5V - powered directly from the ESP32's 3.3V pin. |
| 2x bare male DE-9 connector shells | Wiring flexibility, isolating power pins from the signal path | One mates with the printer-side breakout (carries all of pins 2/3/5/6/7/8/9), one mates with the MAX3232 module's socket (carries **only** pins 2/3/5 - nothing else is ever wired to it) |
| DE-9 breakout board (screw terminals) | Junction point for the printer cable + power injection | From TinyTronics; only pins 2/3/5/6/7/8/9 get wired (1/4 unused) |
| LOLIN D1 Mini ESP32 (WROOM-32) | Runs the HTTP -> UART bridge over WiFi | Plain WROOM is deliberate: no PSRAM, so GPIO16/17 are free for the UART. USB-serial chip is a CH9102 (enumerates as a COM port; needs the CH34x/CH9102 driver on some Windows setups). PlatformIO board id `wemos_d1_mini32`. |
| IKEA SJÖSS 65W USB-C charger + USB-C PD trigger board (set to 12V) | Power source | Charger supports 12V @ 3A (36W) - comfortably covers the printer's 2A rating plus the ESP32. [TinyTronics PD trigger module](https://www.tinytronics.nl/en/power/power-supplies/usb-pd/usb-pd-trigger-module) |
| DFRobot 7-24V -> 5V 4A buck converter | Steps the 12V rail down for the ESP32 | [TinyTronics link](https://www.tinytronics.nl/en/power/voltage-converters/buck-(step-down)-converters/dfrobot-dc-dc-buck-converter-7-24v-to-5v-4a) |

(A dedicated 12V/3A wall adapter, e.g. TinyTronics' "Sunshine" adapter line, works just as well instead of the USB-C PD charger + trigger board, if that's simpler to source.)

## Wiring plan

```
[PSU +12V] --------------------------> printer-side breakout: pins 6 + 7 (tied)
[PSU GND]  --------------------------> printer-side breakout: pins 8 + 9 (tied)

printer-side breakout: pin 2 (RXD) --> 3 loose wires --> male DE-9 shell #2, pin 2 --> [MAX3232 module socket]
printer-side breakout: pin 3 (TXD) -->                --> male DE-9 shell #2, pin 3
printer-side breakout: pin 5 (GND) -->                --> male DE-9 shell #2, pin 5
                                                            (pins 1/4/6/7/8/9 on shell #2 are left empty)

[this whole breakout] ===(mates via the original cable)===> [printer]

MAX3232 module TTL header:
   VCC --> ESP32 3V3 pin      (regulated 3.3V *out* of the board - NOT 5V.
                               The module's TTL levels follow its VCC, and
                               ESP32 GPIOs are not 5V-tolerant.)
   GND --> ESP32 GND
   TXD --> ESP32 GPIO16 (RX)
   RXD --> ESP32 GPIO17 (TX)
   (RTS/CTS unused - no handshake needed)

[PSU +12V] --> buck converter in --> buck converter out (5V) --> ESP32 5V/VIN pin
[PSU GND]  --> buck converter GND, and common with ESP32 GND
```

The board is fed 5V on its `5V`/`VIN` pin; its onboard regulator then produces
the 3.3V that powers the MAX3232 module off the `3V3` pin. GPIO16/17 are the UART
pins fixed in the firmware ([`config.h`](../firmware/include/config.example.h) sets
baud; the pins are in `main.cpp`): plain general-purpose pins, no strapping
function, no boot-time side effects.

### Confirmed during bring-up

- **RS-232 needs a 2<->3 crossover between the module and the printer.** This
  SP3232 module drives its RS-232 TX on **DE-9 pin 2**; the Woosim receives on
  **pin 3** (it's wired DCE). Straight-through = nothing prints. The old
  photobooth cable was straight 2-2/3-3/5-5 and worked only because its host (a
  standard DTE USB-serial adapter) transmits on pin 3. Swap 2<->3 on one end of
  the custom cable, or use an inline null-modem adapter. GND stays 5-5; power
  stays on 6/7/8/9.
- **Baud is 9600 8N1, no handshake** - matches `PRINTER_BAUD` / `SerialWoosimPrinter`.
- Verify end to end with the CLI, which exercises the real encoder + transport:
  `dotnet run -- test --printer network --host woosim-printer.local:5251`
  (from `src/ReceiptPrinter.CLI`). Prefer this over ad-hoc `curl`/PowerShell -
  hand-built HTTP bodies are easy to get wrong (e.g. PowerShell stringifies a
  non-`byte[]` body, sending decimal ASCII instead of raw bytes).

Key safety point: power (12V/2A, pins 6-9) and signal (pins 2/3/5) are **electrically separate paths from the printer-side breakout onward**. The MAX3232 module and the ESP32 never see the power pins at all - only 3 dedicated wires (RXD/TXD/GND) reach the module, via a second bare DE-9 shell that has nothing else wired to it. This is what makes it safe to use a MAX3232 module that happens to have its own DE-9 socket, despite the original cable carrying power on the same physical connector.

## Flashing the firmware

The firmware project is [`../firmware/`](../firmware/) (PlatformIO + Arduino-ESP32).
Full details in [`../firmware/README.md`](../firmware/README.md); the short version:

1. **WiFi credentials** go in `firmware/include/config.h` - copy it from
   `config.example.h` and edit `WIFI_SSID` / `WIFI_PASSWORD`. `config.h` is
   git-ignored; only the example is committed, so creds never land in git.
2. **First flash is over USB.** Connect the board, then from `firmware/`:
   ```
   pio run -t upload && pio device monitor
   ```
   PlatformIO auto-detects the CH9102 COM port. If the upload hangs at
   `Connecting....`, hold the board's **BOOT** button until it starts writing.
3. The serial monitor (115200 baud) prints the assigned IP on boot. The device
   also advertises **`printer.local`** over mDNS - which is exactly the sender's
   default (`printer.local:5251`), so no config change is needed on the app side.
4. **Later flashes can go over WiFi** (OTA): uncomment the `espota` block in
   `firmware/platformio.ini`.

Smoke test without the app:
```
curl -sS http://printer.local:5251/health
printf '\x1b@Hello\n\n\n\x1dV\x00' | curl -sS --data-binary @- \
  -H 'Content-Type: application/octet-stream' http://printer.local:5251/print
```

Then point the sender at it: set **`Printer.NetworkHost`** to `printer.local:5251`
(add-on Configuration tab, or `--host` on the CLI) instead of the PC running
`NetworkSerialService`.

## Where to buy (NL)

- [TinyTronics](https://www.tinytronics.nl) - MAX3232 module, ESP32 boards, buck converter, DE-9 connectors/breakouts, PD trigger module.
- [Antratek](https://www.antratek.nl) - alternative source for MAX3232 boards (Seeed Studio / RS232 Click).
- [Kiwi Electronics](https://www.kiwi-electronics.com) - alternative power supplies.

## Notes / gotchas

- **RS-232 pinout** (DE-9, DTE): 1 DCD, 2 RXD, 3 TXD, 4 DTR, 5 GND, 6 DSR, 7 RTS, 8 CTS, 9 RI. The printer's cable repurposes 6/7/8/9 for power, ignoring their usual modem-control meaning.
- **Apple Reminders + CalDAV**: iCloud exposes Reminders lists over CalDAV, but only the original default list (created before Reminders' CloudKit-based redesign) is actually visible that way. Lists created later - even though they sync fine to iCloud.com and other devices - are invisible to CalDAV entirely. This is a known Apple-side limitation (other CalDAV clients like Thunderbird/DAVx5/BusyCal hit the same wall), not something fixable from our end. A direct CalDAV client for the default list was tried and removed again once it turned out not to work reliably in practice; the actual to-do list in the daily briefing is sourced via Home Assistant + an iOS Shortcut instead (see the main README).
- Home Assistant's `input_text` helper has a hard 255-character cap - too small for more than a handful of to-do items. The to-do list is instead stored as an **attribute** on a trigger-based template sensor (`sensor.todo_list`, `items` attribute), which has no such limit.

## Home Assistant setup: the to-do webhook

The full path is `iPhone Shortcut -> HA webhook -> template sensor -> this app polls the sensor` (see the main README's [to-do list data flow](../README.md#to-do-list-data-flow)). The Home Assistant side is a webhook-triggered template sensor added to `configuration.yaml`:

```yaml
template:
  - trigger:
      - trigger: webhook
        webhook_id: todo_update
        local_only: true
        allowed_methods:
          - POST
          - PUT
    sensor:
      - name: "Todo List"
        state: "{{ now() }}"
        attributes:
          items: "{{ trigger.json.todos }}"
```

- `state` is just the last-updated timestamp - the actual list lives in the `items` attribute, since attributes aren't subject to the 255-character state cap.
- `trigger.json.todos` expects the webhook body to be JSON with a `todos` key, e.g. `{"todos": "Buy milk\nCall dentist\n..."}` - one item per line, which is exactly what an iOS Shortcut's "Combine Text" (newline-separated) produces.
- The webhook URL is `<ha-base-url>/api/webhook/todo_update` - point the iOS Shortcut's "Get Contents of URL" (POST, JSON body `{"todos": <combined text>}`) at that.
- After a reload (Settings -> System -> Restart, or Developer Tools -> YAML -> Template Entities), `sensor.todo_list`'s `items` attribute is what `ha-config.json`'s `EntityId`/`AttributeName` should point at (see the main README's config files section).
