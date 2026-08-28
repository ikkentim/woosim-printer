# MQTT entities

The Service uses MQTT discovery to register entities with Home Assistant. Add-ons can't register real Home Assistant services/actions directly - only a custom integration can, and there's no HTTP API either - so **MQTT discovery** is the only way in. 

A broker (e.g. the official Mosquitto add-on) is required. `MqttAddonService` resolves it via Supervisor's Services API (`SUPERVISOR_TOKEN`, no user-entered broker config needed) and publishes retained discovery configs for a single "Receipt Printer Service" device.

## Available entities

| Entity | Type | Behavior |
|---|---|---|
| `button.receipt_printer_print_daily_briefing` | Button | Builds and prints the daily briefing |
| `button.receipt_printer_check_to_dos_now` | Button | Runs the to-do note checker |
| `notify.receipt_printer_print` | Notify | `notify.send_message` prints the message - see [Custom print formatting](#custom-print-formatting) below |
| `binary_sensor.receipt_printer_printer_reachable` | Binary sensor | Polled every minute via `IReceiptPrinter.PingAsync()` - never prints anything; `on`/`off` indicates if the printer is reachable |

## Using in automations

An automation just looks like:

```yaml
- action: button.press
  target:
    entity_id: button.receipt_printer_print_daily_briefing
```

No hardcoded add-on hostname, no YAML beyond the automation itself.

> If no MQTT broker is configured in Home Assistant at all, the add-on has nothing to do - it logs this and idles.

## Custom print formatting

`notify.receipt_printer_print`'s message goes through [`ReceiptMarkdown`](../src/ReceiptPrinter.Shared/Receipts/ReceiptMarkdown.cs), a tiny receipt-specific dialect - just enough to make a printed note look intentional, entirely in the message string (no `data:` fields, no JSON):

### Formatting rules

- A line that's just `~~~` requests a full cut instead of the default partial one, and prints nothing for that line.
- A line that's just `[WidgetName]` (e.g. `[Weather]`, `[Calendar]`) splices in that briefing widget's own live output - the same widgets/factories the daily briefing itself uses, including `[DailyBriefing]` for the whole thing.
- A line that's just `![name]` prints a bundled weather glyph, centered (`![sunny]`, `![partlycloudy]`, `![rainy]`, `![pouring]`, `![snowy]`, `![fog]`, `![lightning]`, `![windy]`, `![clear-night]`, ... - the Home Assistant weather conditions). Unknown names print nothing.
- A line starting with `>>` right-justifies; `>` centers; otherwise left (default) - checked before the heading marker, so `> # Heading` is a centered heading.
- A line starting with `# ` prints big and bold (e.g. `# Maandag` as a heading).
- `**bold**` and `*underline*` toggle for the enclosed text - freely mixable/nestable, multiple times per line.
- `\*`, `\#`, `\>`, `\~`, `\\` escape to a literal character.

### Example

```yaml
- action: notify.send_message
  target:
    entity_id: notify.receipt_printer_print
  data:
    message: |-
      # Grocery run
      **Milk**, eggs, bread
      *don't forget the receipt*
      ![rainy]
      [Weather]
      ~~~
```
