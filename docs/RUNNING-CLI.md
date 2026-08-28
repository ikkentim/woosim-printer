# Running the CLI

Both the CLI and the Service read settings from the same place - see [Configuration](CONFIGURATION.md).

## Commands

```bash
cd src/ReceiptPrinter.CLI

dotnet run -- --help                          # full command/option reference
dotnet run -- test                            # basic ESC/POS test print, serial/COM3/9600 by default
dotnet run -- test --printer serial --port COM3 --baud 9600   # same, explicit
dotnet run -- test --printer network --host printer-pc.local:5251  # over the network, via ReceiptPrinter.NetworkSerialService

dotnet run -- briefing                      # the daily briefing, serial by default (same --printer/--port/--baud/--host options as test)

dotnet run -- print "**Milk**, eggs, bread"          # ReceiptMarkdown text as an argument
printf "# Grocery run\n**Milk**, eggs, bread\n" | dotnet run -- print   # ...or piped via stdin, if no argument is given
```

## Options

- `--printer` - `serial` or `network`
- `--port` - serial port name (e.g., `COM3` on Windows, `/dev/ttyUSB0` on Linux)
- `--baud` - serial baud rate (default: `9600`)
- `--host` - network service host:port (e.g., `printer-pc.local:5251`)
