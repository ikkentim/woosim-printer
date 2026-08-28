# To-do list data flow

Apple Reminders lists created after Reminders' CloudKit-based redesign don't get exposed over CalDAV, so pulling them server-side isn't possible. The workaround:

```
iPhone (Shortcuts: "Find Reminders" -> Combine Text -> Get Contents of URL)
   -> Home Assistant webhook
   -> trigger-based template sensor (sensor.todo_list, "items" attribute - no length cap)
   -> this app polls the HA REST API when printing
```

## Home Assistant setup: the webhook sensor

Add this to your Home Assistant `configuration.yaml`:

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

### Details

- `state` is just the last-updated timestamp - the actual list lives in the `items` attribute, since attributes aren't subject to the 255-character state cap.
- `trigger.json.todos` expects the webhook body to be JSON with a `todos` key, e.g. `{"todos": "Buy milk\nCall dentist\n..."}` - one item per line, which is exactly what an iOS Shortcut's "Combine Text" (newline-separated) produces.
- The webhook URL is `<ha-base-url>/api/webhook/todo_update` - point the iOS Shortcut's "Get Contents of URL" (POST, JSON body `{"todos": <combined text>}`) at that.
- After a reload (Settings -> System -> Restart, or Developer Tools -> YAML -> Template Entities), `sensor.todo_list`'s `items` attribute is what `ReceiptPrinterOptions.HomeAssistant.TodoEntityId` / `.TodoAttributeName` should point at (see [Configuration](CONFIGURATION.md)).

## Why CalDAV doesn't work

iCloud exposes Reminders lists over CalDAV, but only the original default list (created before Reminders' CloudKit-based redesign) is actually visible that way. Lists created later - even though they sync fine to iCloud.com and other devices - are invisible to CalDAV entirely. This is a known Apple-side limitation (other CalDAV clients like Thunderbird/DAVx5/BusyCal hit the same wall), not something fixable from our end.

A direct CalDAV client for the default list was tried and removed again once it turned out not to work reliably in practice.
