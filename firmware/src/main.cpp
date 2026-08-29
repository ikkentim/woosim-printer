// Standalone receipt-printer firmware for a LOLIN D1 Mini ESP32.
//
// Replaces "PC + USB-serial adapter" with an ESP32 on the LAN. It does exactly
// what ReceiptPrinter.NetworkSerialService does in C#, and nothing more:
//
//   POST /print   -> stream the raw request body to the printer UART, byte for byte
//   GET  /health  -> 200 "ok", never touches the printer
//
// The body is a ready-made ESC/POS byte stream produced by EscPosEncoder on the
// sender, so this side needs no knowledge of receipts, JSON, or ESC/POS.
//
// The HTTP is hand-rolled on a plain WiFiServer rather than a library: the
// protocol is two routes, the only client is NetworkWoosimPrinter (HttpClient,
// always Content-Length, never chunked), and a raw socket read is binary-safe -
// ESC/POS streams are full of NUL bytes that String-based body parsers truncate.
//
// Wiring to the SP3232/MAX3232 module (see docs/HARDWARE.md):
//
//   ESP32 GPIO16 (RX) <--- module TXD
//   ESP32 GPIO17 (TX) ---> module RXD
//   ESP32 3V3         ---> module VCC      (module at 3.3V so its TTL side
//   ESP32 GND         ---> module GND       matches the ESP32; never 5V)
//   RTS/CTS unused - no handshake.

#include <Arduino.h>
#include <WiFi.h>
#include <ESPmDNS.h>
#include <ArduinoOTA.h>

#include "config.h"

#ifndef PRINTER_BAUD
#define PRINTER_BAUD 9600
#endif
#ifndef HTTP_PORT
#define HTTP_PORT 5251
#endif
#ifndef MDNS_HOSTNAME
#define MDNS_HOSTNAME "printer"
#endif

// --- UART -----------------------------------------------------------------
// GPIO16/17 are safe general-purpose pins on the WROOM-32 (no PSRAM eating
// them), no strapping function, no boot-time side effects.
static const int kUartRxPin = 16;
static const int kUartTxPin = 17;
#define PRINTER_UART Serial2

// Live baud, seeded from config. `/print?baud=N` and `/selftest?baud=N` change
// it at runtime (survives until the next override or reboot) so the printer's
// rate can be found without a reflash. Set PRINTER_BAUD in config.h once known.
static uint32_t gBaud = PRINTER_BAUD;

static void applyBaud(uint32_t baud) {
  if (baud == gBaud) return;
  gBaud = baud;
  PRINTER_UART.updateBaudRate(baud);
  Serial.printf("uart: baud -> %u\n", baud);
}

// Pull "baud=N" out of a raw query string ("a=1&baud=19200"); 0 if absent/bad.
static uint32_t parseBaudArg(const String &query) {
  int at = query.indexOf("baud=");
  if (at < 0) return 0;
  long v = query.substring(at + 5).toInt();
  return (v >= 300 && v <= 1000000) ? (uint32_t)v : 0;
}

// --- limits -------------------------------------------------------------
static const uint32_t kClientTimeoutMs = 10000;      // per stalled read/line
static const size_t kMaxBodyBytes = 128 * 1024;      // reject runaway uploads
static const size_t kMaxHeaderLine = 2048;

WiFiServer listener(HTTP_PORT);

// ======================================================================
// WiFi
// ======================================================================

static void connectWifi() {
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);                 // keep latency low for incoming POSTs
  WiFi.setHostname(MDNS_HOSTNAME);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  Serial.printf("wifi: joining \"%s\"", WIFI_SSID);
  uint32_t start = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - start < 20000) {
    delay(250);
    Serial.print('.');
  }
  Serial.println();

  if (WiFi.status() == WL_CONNECTED)
    Serial.printf("wifi: %s  mac %s\n",
                  WiFi.localIP().toString().c_str(), WiFi.macAddress().c_str());
  else
    Serial.println("wifi: not connected yet - will keep retrying in loop()");
}

// Non-blocking reconnect: nudge the supplicant every 5s while disconnected.
static void keepWifiAlive() {
  static uint32_t lastTry = 0;
  if (WiFi.status() == WL_CONNECTED) return;
  if (millis() - lastTry < 5000) return;
  lastTry = millis();
  Serial.println("wifi: reconnecting");
  WiFi.disconnect();
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
}

// ======================================================================
// Tiny HTTP/1.1 server
// ======================================================================

// Read one CRLF-terminated line, sans terminator. false on timeout/disconnect.
static bool readLine(WiFiClient &client, String &out, uint32_t deadline) {
  out = "";
  while (millis() < deadline) {
    while (client.available()) {
      char c = (char)client.read();
      if (c == '\n') {
        if (out.endsWith("\r")) out.remove(out.length() - 1);
        return true;
      }
      out += c;
      if (out.length() > kMaxHeaderLine) return false;
    }
    if (!client.connected() && !client.available()) return false;
    delay(1);
  }
  return false;
}

static void sendStatus(WiFiClient &client, int code, const char *reason,
                       const char *body) {
  client.printf("HTTP/1.1 %d %s\r\n", code, reason);
  client.print("Content-Type: text/plain\r\n");
  client.printf("Content-Length: %u\r\n", (unsigned)strlen(body));
  client.print("Connection: close\r\n\r\n");
  client.print(body);
}

// Pump exactly `length` bytes from the socket straight to the printer UART.
// UART write() blocks once the TX FIFO is full, so this self-paces at 9600 baud
// - no flow control needed, same as the C# side.
static bool streamBodyToPrinter(WiFiClient &client, long length) {
  uint8_t buf[512];
  uint32_t deadline = millis() + kClientTimeoutMs;

  while (length > 0) {
    if (millis() >= deadline) return false;

    long want = length < (long)sizeof(buf) ? length : (long)sizeof(buf);
    int n = client.read(buf, want);
    if (n <= 0) {
      if (!client.connected() && !client.available()) return false;
      delay(1);
      continue;
    }

    size_t written = 0;
    while (written < (size_t)n) {
      written += PRINTER_UART.write(buf + written, (size_t)n - written);
      yield();
    }
    length -= n;
    deadline = millis() + kClientTimeoutMs;   // forward progress resets it
  }

  PRINTER_UART.flush();                        // wait for the last byte to leave
  return true;
}

static void handleClient(WiFiClient client) {
  client.setNoDelay(true);
  uint32_t deadline = millis() + kClientTimeoutMs;

  String requestLine;
  if (!readLine(client, requestLine, deadline)) { client.stop(); return; }

  int sp1 = requestLine.indexOf(' ');
  int sp2 = requestLine.indexOf(' ', sp1 + 1);
  if (sp1 < 0 || sp2 < 0) {
    sendStatus(client, 400, "Bad Request", "malformed request line\n");
    client.stop();
    return;
  }
  String method = requestLine.substring(0, sp1);
  String path = requestLine.substring(sp1 + 1, sp2);

  long contentLength = -1;
  String header;
  for (;;) {
    if (!readLine(client, header, deadline)) { client.stop(); return; }
    if (header.isEmpty()) break;               // blank line => headers done
    String lower = header;
    lower.toLowerCase();
    if (lower.startsWith("content-length:")) {
      contentLength = header.substring(header.indexOf(':') + 1).toInt();
    }
  }

  String query;
  int q = path.indexOf('?');
  if (q >= 0) { query = path.substring(q + 1); path = path.substring(0, q); }

  uint32_t baudArg = parseBaudArg(query);
  if (baudArg) applyBaud(baudArg);

  Serial.printf("http: %s %s  (len %ld, baud %u)\n", method.c_str(),
                path.c_str(), contentLength, gBaud);

  if (method == "GET" && path == "/health") {
    sendStatus(client, 200, "OK", "ok");
    client.stop();
    return;
  }

  // Diagnostic: write a known pattern out TX and report whatever comes back on
  // RX within 300 ms. Jumper the two UART pins (or the RS-232 pair) to prove a
  // segment in isolation. Never touches HTTP-vs-printer logic.
  if (method == "GET" && path == "/selftest") {
    while (PRINTER_UART.available()) PRINTER_UART.read();   // drain stale bytes

    static const char probe[] = "UART-LOOPBACK-0123456789";
    const size_t probeLen = sizeof(probe) - 1;
    PRINTER_UART.write(reinterpret_cast<const uint8_t *>(probe), probeLen);
    PRINTER_UART.flush();

    uint8_t rx[64];
    size_t got = 0;
    uint32_t until = millis() + 300;
    while (millis() < until && got < sizeof(rx)) {
      while (PRINTER_UART.available() && got < sizeof(rx))
        rx[got++] = (uint8_t)PRINTER_UART.read();
      delay(1);
    }

    String report;
    report.reserve(256);
    report += "sent " + String((unsigned)probeLen) + " bytes, received " +
              String((unsigned)got) + "\nrx ascii: ";
    for (size_t i = 0; i < got; i++)
      report += (rx[i] >= 32 && rx[i] < 127) ? (char)rx[i] : '.';
    report += "\nrx hex  : ";
    for (size_t i = 0; i < got; i++) {
      char b[4];
      snprintf(b, sizeof(b), "%02X ", rx[i]);
      report += b;
    }
    report += '\n';
    report += (got == probeLen && memcmp(rx, probe, got) == 0)
                  ? "RESULT: loopback OK - Serial2 TX+RX+GPIO16/17 all good\n"
                  : "RESULT: no / partial echo\n";

    sendStatus(client, 200, "OK", report.c_str());
    client.stop();
    return;
  }

  if (method == "POST" && path == "/print") {
    if (contentLength < 0) {
      sendStatus(client, 411, "Length Required", "need content-length\n");
    } else if (contentLength == 0) {
      sendStatus(client, 400, "Bad Request", "empty body\n");
    } else if (contentLength > (long)kMaxBodyBytes) {
      sendStatus(client, 413, "Payload Too Large", "body too large\n");
    } else if (streamBodyToPrinter(client, contentLength)) {
      sendStatus(client, 200, "OK", "ok");
      Serial.printf("http: printed %ld bytes\n", contentLength);
    } else {
      sendStatus(client, 408, "Request Timeout", "short read from client\n");
      Serial.println("http: aborted - short read");
    }
    client.stop();
    return;
  }

  sendStatus(client, 404, "Not Found", "not found\n");
  client.stop();
}

// ======================================================================
// setup / loop
// ======================================================================

void setup() {
  Serial.begin(115200);
  delay(200);
  Serial.println("\nreceipt-printer firmware");

  PRINTER_UART.begin(gBaud, SERIAL_8N1, kUartRxPin, kUartTxPin);
  Serial.printf("uart: Serial2 @ %u 8N1  (RX=GPIO%d TX=GPIO%d)\n",
                gBaud, kUartRxPin, kUartTxPin);

  connectWifi();

  if (MDNS.begin(MDNS_HOSTNAME)) {
    MDNS.addService("http", "tcp", HTTP_PORT);
    Serial.printf("mdns: http://%s.local:%d/\n", MDNS_HOSTNAME, HTTP_PORT);
  }

  ArduinoOTA.setHostname(MDNS_HOSTNAME);
#ifdef OTA_PASSWORD
  ArduinoOTA.setPassword(OTA_PASSWORD);
#endif
  ArduinoOTA.onStart([]() { Serial.println("ota: update starting"); });
  ArduinoOTA.onEnd([]() { Serial.println("ota: done"); });
  ArduinoOTA.begin();

  listener.begin();
  listener.setNoDelay(true);
  Serial.printf("http: listening on :%d  (POST /print, GET /health)\n",
                HTTP_PORT);
}

void loop() {
  ArduinoOTA.handle();
  keepWifiAlive();

  WiFiClient client = listener.accept();       // one at a time = serialized jobs
  if (client) handleClient(client);
}
