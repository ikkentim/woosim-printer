#pragma once

// Copy this file to `config.h` and fill in your details.
// `config.h` is git-ignored so credentials never get committed.

// --- WiFi ---------------------------------------------------------------
#define WIFI_SSID      "your-ssid"
#define WIFI_PASSWORD  "your-wifi-password"

// --- Identity ---------------------------------------------------------------
// mDNS + OTA hostname. The sender defaults to "printer.local:5251"
// (src/ReceiptPrinter.CLI/Program.cs), so "printer" works with no extra config.
#define MDNS_HOSTNAME  "printer"

// TCP port the HTTP server listens on. Keep in sync with the sender's host:port.
#define HTTP_PORT      5251

// --- Printer UART ---------------------------------------------------------
// Woosim thermal printer: 9600 baud, 8N1, no handshake (matches
// SerialWoosimPrinter.cs). Wiring is fixed in main.cpp: GPIO16 = RX, GPIO17 = TX.
#define PRINTER_BAUD   9600

// --- OTA updates (optional) --------------------------------------------
// Password required to push firmware over the network. Comment this line out
// to allow unauthenticated OTA (only sane on a trusted LAN), or to disable the
// hint in platformio.ini.
#define OTA_PASSWORD   "change-me"
