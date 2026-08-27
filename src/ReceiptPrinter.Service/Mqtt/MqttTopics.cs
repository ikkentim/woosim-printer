namespace ReceiptPrinter.Service.Mqtt;

/// <summary>
/// All the topic names the MQTT integration uses, in one place so discovery configs and the
/// subscribe/publish calls that back them can't drift apart.
/// </summary>
public static class MqttTopics
{
    private const string Base = "receiptprinter";
    private const string DiscoveryPrefix = "homeassistant";

    public const string AvailabilityTopic = $"{Base}/status/availability";
    public const string ReachableStateTopic = $"{Base}/status/reachable";

    public const string BriefingCommandTopic = $"{Base}/briefing/press";
    public const string TodoCheckCommandTopic = $"{Base}/todo_check/press";
    public const string PrintCommandTopic = $"{Base}/print/message";

    public const string BriefingDiscoveryTopic = $"{DiscoveryPrefix}/button/{Base}/briefing/config";
    public const string TodoCheckDiscoveryTopic = $"{DiscoveryPrefix}/button/{Base}/todo_check/config";
    public const string PrintDiscoveryTopic = $"{DiscoveryPrefix}/notify/{Base}/print/config";
    public const string ReachableDiscoveryTopic = $"{DiscoveryPrefix}/binary_sensor/{Base}/reachable/config";

    // Every entity shares this so they group under a single "Receipt Printer Service" device in HA,
    // rather than showing up as unrelated loose entities.
    private static readonly object Device = new
    {
        identifiers = new[] { "receiptprinter_service" },
        name = "Receipt Printer Service",
        manufacturer = "ikkentim/woosim-printer",
        model = "Woosim Receipt Printer",
    };

    public static object BriefingButtonConfig() => new
    {
        name = "Print Daily Briefing",
        unique_id = "receiptprinter_briefing",
        command_topic = BriefingCommandTopic,
        availability_topic = AvailabilityTopic,
        icon = "mdi:receipt-text-clock",
        device = Device,
    };

    public static object TodoCheckButtonConfig() => new
    {
        name = "Check To-Dos Now",
        unique_id = "receiptprinter_todo_check",
        command_topic = TodoCheckCommandTopic,
        availability_topic = AvailabilityTopic,
        icon = "mdi:clipboard-check-outline",
        device = Device,
    };

    public static object PrintNotifyConfig() => new
    {
        name = "Print",
        unique_id = "receiptprinter_print",
        command_topic = PrintCommandTopic,
        availability_topic = AvailabilityTopic,
        icon = "mdi:printer",
        device = Device,
    };

    public static object ReachableSensorConfig() => new
    {
        name = "Printer Reachable",
        unique_id = "receiptprinter_reachable",
        state_topic = ReachableStateTopic,
        payload_on = "ON",
        payload_off = "OFF",
        device_class = "connectivity",
        availability_topic = AvailabilityTopic,
        device = Device,
    };
}
