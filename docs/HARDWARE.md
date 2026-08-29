# Hardware: the standalone ESP32 printer

The printer is an old Woosim serial thermal receipt printer, salvaged from a
photobooth, now driven by an ESP32 on the LAN. The ESP32 runs a deliberately dumb
HTTP -> UART bridge ([`../firmware/`](../firmware/)); the sender does all the
ESC/POS work and POSTs the bytes to it.

## Background

The photobooth wired the printer as a bodge job: a single DE-9 (RS-232) cable
carries both the data signals *and* injected DC power, since that build never
gave the printer a separate power connector.

- **Originally:** printer -> DE-9 cable -> USB-to-serial adapter -> PC, 9600 baud,
  with a 12V supply injected into the same DE-9 cable downstream of the adapter.
- **Now:** the PC + USB-serial adapter are replaced by an ESP32 + a MAX3232 level
  shifter. The 12V injection is unchanged. Home Assistant automations reach the
  printer directly over WiFi/HTTP, no PC involved.

## Firmware scope

The firmware stays deliberately dumb. All the receipt -> ESC/POS translation
happens on the sender (`ReceiptPrinter.Network` / `EscPosEncoder`); the ESP32
only has to:

- serve `POST /print` - read the `application/octet-stream` request body and write it to the UART, byte for byte
- serve `GET /health` - return `200` without touching the printer

That's the whole contract. It's implemented twice: [`../firmware/src/main.cpp`](../firmware/src/main.cpp)
for the ESP32, and `ReceiptPrinter.NetworkSerialService` as a line-for-line C#
reference that still works as a PC-side stand-in. Build and flash details live in
[`../firmware/README.md`](../firmware/README.md).

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

## Wiring

```
[PSU +12V] --------------------------> printer-side breakout: pins 6 + 7 (tied)
[PSU GND]  --------------------------> printer-side breakout: pins 8 + 9 (tied)

printer-side breakout: pins 2, 3, 5 --> 3 loose wires --> male DE-9 shell #2 --> [MAX3232 module socket]
                                        (pin 5 -> 5 straight; pins 2 and 3 CROSSED,
                                         see "RS-232 crossover" below.
                                         Pins 1/4/6/7/8/9 on shell #2 left empty.)

[this whole breakout] ===(mates via the original cable)===> [printer]

MAX3232 module TTL header:
   VCC --> ESP32 3V3 pin      (regulated 3.3V *out* of the board - NOT 5V.
                               The module's TTL levels follow its VCC, and
                               ESP32 GPIOs are not 5V-tolerant.)
   GND --> ESP32 GND
   TXD --> ESP32 GPIO16 (RX)
   RXD --> ESP32 GPIO17 (TX)
   (RTS/CTS unused - no handshake)

[PSU +12V] --> buck converter in --> buck converter out (5V) --> ESP32 5V/VIN pin
[PSU GND]  --> buck converter GND, and common with ESP32 GND
```

The board is fed 5V on its `5V`/`VIN` pin; its onboard regulator produces the
3.3V that powers the MAX3232 module off the `3V3` pin. GPIO16/17 are plain
general-purpose pins - no strapping function, no boot-time side effects; the
firmware fixes the pins in `main.cpp` and the baud in `config.h`.

**Serial settings: 9600 baud, 8N1, no handshake.**

### RS-232 crossover

The signal run between the MAX3232 module and the printer must **swap pins 2 and
3**. This SP3232 module drives its RS-232 TX on **DE-9 pin 2**; the Woosim
receives on **pin 3** (it's wired DCE). Straight-through prints nothing (or, with
a byte stream at the wrong rate, garbage).

The old photobooth cable is straight (2-2, 3-3, 5-5) and worked only because its
host - a standard DTE USB-serial adapter - transmits on pin 3. Swapping in the
ESP32 + this module moves TX to pin 2, hence the crossover. GND stays 5-5; the
12V injection on 6/7/8/9 is untouched. An inline null-modem adapter works just as
well as re-pinning the cable.

### Power / signal isolation

Power (12V/2A, pins 6-9) and signal (pins 2/3/5) stay **electrically separate
from the printer-side breakout onward**. The MAX3232 module and ESP32 never see
the power pins - only 3 wires (RXD/TXD/GND) reach the module, via a second bare
DE-9 shell with nothing else wired to it. That's what makes it safe to use a
MAX3232 module that happens to carry its own DE-9 socket, despite the original
cable carrying power on the same physical connector.

## Flashing the firmware

Full steps in [`../firmware/README.md`](../firmware/README.md). In short:

1. `cp firmware/include/config.example.h firmware/include/config.h` and set
   `WIFI_SSID` / `WIFI_PASSWORD`. `config.h` is git-ignored; only the example is
   committed, so creds never land in git.
2. **First flash over USB**, from `firmware/`:
   ```
   pio run -t upload && pio device monitor
   ```
   Auto-detect can grab the wrong COM port - pass `--upload-port COMx` if needed,
   and hold the board's **BOOT** button if it hangs at `Connecting....`.
3. The device joins WiFi and advertises **`printer.local`** over mDNS (hostname =
   `MDNS_HOSTNAME` in `config.h`; the sender defaults to `printer.local:5251`).
   The serial monitor (115200 baud) prints the assigned IP on boot.
4. **Later flashes can go over WiFi:** `pio run -e ota -t upload` (OTA auth comes
   from the git-ignored `firmware/ota.local.ini` - see `ota.local.ini.example`).

Verify end to end with the CLI, which exercises the real encoder + transport:

```
dotnet run -- test --printer network --host printer.local:5251   # from src/ReceiptPrinter.CLI
```

Prefer this over ad-hoc `curl`/PowerShell: hand-built HTTP bodies are easy to get
wrong (PowerShell in particular stringifies a non-`byte[]` body, sending decimal
ASCII instead of raw bytes).

Then point the sender at it permanently: set **`Printer.NetworkHost`** to
`printer.local:5251` (add-on Configuration tab, or `--host` on the CLI) instead of
a PC running `NetworkSerialService`.

## Where to buy (NL)

- [TinyTronics](https://www.tinytronics.nl) - MAX3232 module, ESP32 boards, buck converter, DE-9 connectors/breakouts, PD trigger module.
- [Antratek](https://www.antratek.nl) - alternative source for MAX3232 boards (Seeed Studio / RS232 Click).
- [Kiwi Electronics](https://www.kiwi-electronics.com) - alternative power supplies.

## Notes / gotchas

- **RS-232 pinout** (DE-9, DTE): 1 DCD, 2 RXD, 3 TXD, 4 DTR, 5 GND, 6 DSR, 7 RTS, 8 CTS, 9 RI. The printer's cable repurposes 6/7/8/9 for power, ignoring their usual modem-control meaning.
- The to-do list plumbing (Apple Reminders, the Home Assistant webhook, the template sensor) has nothing to do with the hardware - see [To-do list data flow](TODO-DATA-FLOW.md).
