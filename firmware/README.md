# ESP32 printer firmware

Turns a LOLIN **D1 Mini ESP32** into the standalone HTTP -> UART bridge planned
in [`../docs/HARDWARE.md`](../docs/HARDWARE.md). It replaces the PC +
`ReceiptPrinter.NetworkSerialService`; the sender (`NetworkWoosimPrinter`) talks
to it unchanged.

## Contract

| Route | Behaviour |
|---|---|
| `POST /print` | Request body (`application/octet-stream`, raw ESC/POS) is streamed to the printer UART byte-for-byte. `200 ok` once the last byte has left the UART. |
| `GET /health` | `200 ok`, printer untouched. |
| `GET /selftest` | Writes a known pattern out TX and reports what returns on RX within 300 ms. Diagnostic only. |

Both `/print` and `/selftest` accept `?baud=N` to change the UART rate at runtime
(persists until the next override or reboot) - handy for finding the printer's
rate without a reflash. Once known, set `PRINTER_BAUD` in `config.h`.

Single-threaded: one connection is handled at a time, so concurrent jobs can't
interleave on the wire. `Connection: close` on every response (the sender's
`HttpClient` just opens a fresh connection next time).

## Wiring

```
D1 Mini ESP32            SP3232 / MAX3232 module
  GPIO16 (RX)  <-------  TXD
  GPIO17 (TX)  ------->  RXD
  3V3          ------->  VCC        (3.3V - NOT 5V; TTL levels follow VCC)
  GND          ------->  GND
```

Board is powered via its `VCC`/5V pin from the buck converter; the module runs
off the board's regulated `3V3` pin. RTS/CTS unused.

## First flash (USB)

Uses [PlatformIO](https://platformio.org/) (`pip install platformio`, or the
VS Code extension).

```bash
cd firmware
cp include/config.example.h include/config.h   # then edit WiFi creds
pio run -t upload
pio device monitor                              # 115200 baud
```

On boot the monitor prints the assigned IP. The device also advertises
`printer.local` over mDNS, which is what the sender defaults to
(`printer.local:5251`).

## Later flashes (OTA, over WiFi)

Uncomment the `espota` block in [`platformio.ini`](platformio.ini) (match
`--auth` to `OTA_PASSWORD` in `config.h`), then `pio run -t upload`.

## Test without the app

```bash
curl -sS http://printer.local:5251/health
printf '\x1b@Hello\n\n\n\x1dV\x00' | curl -sS --data-binary @- \
  -H 'Content-Type: application/octet-stream' http://printer.local:5251/print
```

## Bring-up / troubleshooting

`POST /print` returning `200 ok` only proves the bytes left GPIO17. To find a
break between there and the paper, use `GET /selftest` with a jumper:

1. **Jumper GPIO16 <-> GPIO17 directly.** `curl http://printer.local:5251/selftest`
   should report `loopback OK`. Confirms Serial2 + the pins + firmware.
2. **Move the jumper to the module's TTL header** (its RXD <-> TXD). `loopback OK`
   now means the MAX3232's TTL side and its wiring to the ESP32 are fine.
3. **Short DE-9 pins 2 <-> 3** on the cable to the printer. `loopback OK` means
   the RS-232 driver/receiver and the full cable work - so a failure here with
   step 2 passing points at the pin 2/3 orientation or the printer itself.

Other things that bite: module VCC must be ~3.3V (measure it), ESP32/module/
printer must share a ground, and the printer must actually be at 9600 8N1
(some Woosim units default to 19200 or have DIP switches).

**Garbage / random glyphs printing** = the crossover is right (data reaches the
printer) but the baud is wrong. Sweep it with `?baud=`:

```
for b in 9600 19200 38400 57600 115200 4800; do
  curl -sS --data-binary $'\x1b@baud '$b$'\n\n\n' \
    -H 'Content-Type: application/octet-stream' \
    "http://printer.local:5251/print?baud=$b"
  sleep 2
done
```

The rate that prints clean text is the printer's. Most Woosim units also print
their current config (baud included) if powered on while holding **FEED**.

## Notes

- **9600 baud is the flow control.** `Serial2.write()` blocks when the UART TX
  buffer is full, so the firmware can't outrun the printer; matches the
  handshake-less C# path. If very large raster images ever overrun the printer's
  own buffer, the fix is printer-side flow control (wire RTS/CTS through the
  module's second channel), not firmware pacing.
- Body is streamed from socket to UART in 512-byte chunks - no full-receipt
  buffer, and binary-safe (raw socket reads, unlike `String`-based body parsers
  that stop at the first NUL).
- `config.h` is git-ignored. Only `config.example.h` is committed.
- Needs Arduino-ESP32 core 3.x (for `WiFiServer::accept()`); a current
  PlatformIO `espressif32` platform pulls this in.
