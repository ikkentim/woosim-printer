using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Widgets;

namespace ReceiptPrinter.Service.Mqtt;

/// <summary>
/// Publishes MQTT discovery configs so "print the briefing", "check to-dos now" and "print this text"
/// show up as ordinary Home Assistant entities (buttons + a notify target) on a single device, instead
/// of requiring rest_command YAML. Everything here is additive - the HTTP endpoints in Program.cs keep
/// working exactly as before regardless of whether MQTT is available.
///
/// Broker connection details come from Supervisor's Services API (see SupervisorMqttBroker), not from
/// user-entered config - nothing to configure beyond Mqtt.Enabled. If no MQTT broker is set up in Home
/// Assistant at all, this quietly does nothing; it's an enhancement, not a requirement.
/// </summary>
public sealed class MqttAddonService(
    IReceiptPrinter printer,
    TodoNoteChecker todoChecker,
    IOptionsMonitor<ReceiptPrinterOptions> options,
    ILogger<MqttAddonService> logger) : BackgroundService
{
    private static readonly MqttFactory Factory = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.Mqtt.Enabled)
        {
            logger.LogInformation("MQTT integration disabled (Mqtt.Enabled is false) - skipping");
            return;
        }

        var supervisorToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN");
        if (string.IsNullOrEmpty(supervisorToken))
        {
            logger.LogInformation(
                "SUPERVISOR_TOKEN not available - not running as a Home Assistant add-on with API access, " +
                "so MQTT discovery is skipped. The HTTP endpoints still work.");
            return;
        }

        SupervisorMqttBroker? broker;
        try
        {
            broker = await SupervisorMqttBroker.ResolveAsync(supervisorToken, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve MQTT broker from Supervisor - MQTT commands will be unavailable");
            return;
        }

        if (broker == null)
        {
            logger.LogInformation(
                "No MQTT broker registered with Home Assistant Supervisor (install e.g. the Mosquitto " +
                "broker add-on to enable this) - MQTT commands will be unavailable, HTTP endpoints still work.");
            return;
        }

        using var client = Factory.CreateManagedMqttClient();
        client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(broker.Host, broker.Port)
            .WithClientId("receiptprinter-service")
            .WithWillTopic(MqttTopics.AvailabilityTopic)
            .WithWillPayload("offline")
            .WithWillRetain();

        if (!string.IsNullOrEmpty(broker.Username))
            clientOptionsBuilder = clientOptionsBuilder.WithCredentials(broker.Username, broker.Password);

        if (broker.Ssl)
            clientOptionsBuilder = clientOptionsBuilder.WithTlsOptions(tls => tls.UseTls());

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(30))
            .WithClientOptions(clientOptionsBuilder.Build())
            .Build();

        await client.StartAsync(managedOptions);
        logger.LogInformation("Connecting to MQTT broker at {Host}:{Port}", broker.Host, broker.Port);

        await client.SubscribeAsync(MqttTopics.BriefingCommandTopic);
        await client.SubscribeAsync(MqttTopics.TodoCheckCommandTopic);
        await client.SubscribeAsync(MqttTopics.PrintCommandTopic);

        await PublishDiscoveryAsync(client);
        await PublishAsync(client, MqttTopics.AvailabilityTopic, "online", retain: true);

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                var reachable = await SafePingAsync();
                await PublishAsync(client, MqttTopics.ReachableStateTopic, reachable ? "ON" : "OFF", retain: true);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        finally
        {
            await PublishAsync(client, MqttTopics.AvailabilityTopic, "offline", retain: true);
            await client.StopAsync();
        }
    }

    private async Task<bool> SafePingAsync()
    {
        try
        {
            return await printer.PingAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Printer reachability check failed");
            return false;
        }
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;

        try
        {
            switch (topic)
            {
                case MqttTopics.BriefingCommandTopic:
                    logger.LogInformation("MQTT: printing daily briefing on demand");
                    await printer.PrintAsync(await DailyBriefing.BuildAsync(options.CurrentValue));
                    break;

                case MqttTopics.TodoCheckCommandTopic:
                    logger.LogInformation("MQTT: checking to-dos on demand");
                    await todoChecker.CheckAndPrintAsync(printer);
                    break;

                case MqttTopics.PrintCommandTopic:
                    var message = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                    logger.LogInformation("MQTT: printing freeform message ({Length} chars)", message.Length);
                    await printer.PrintAsync(BuildFreeformReceipt(message));
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle MQTT command on {Topic}", topic);
        }
    }

    private static Receipt BuildFreeformReceipt(string message)
    {
        var lines = message.Replace("\r\n", "\n").Split('\n');
        var elements = new List<IElement>();
        elements.AddRange(lines.Select(line => new TextElement(line)));
        elements.Add(new TextElement(""));
        elements.Add(new TextElement(""));

        return new Receipt(elements, CutStyle.Partial);
    }

    private static async Task PublishDiscoveryAsync(IManagedMqttClient client)
    {
        await PublishAsync(client, MqttTopics.BriefingDiscoveryTopic, MqttTopics.BriefingButtonConfig(), retain: true);
        await PublishAsync(client, MqttTopics.TodoCheckDiscoveryTopic, MqttTopics.TodoCheckButtonConfig(), retain: true);
        await PublishAsync(client, MqttTopics.PrintDiscoveryTopic, MqttTopics.PrintNotifyConfig(), retain: true);
        await PublishAsync(client, MqttTopics.ReachableDiscoveryTopic, MqttTopics.ReachableSensorConfig(), retain: true);
    }

    private static Task PublishAsync(IManagedMqttClient client, string topic, object payload, bool retain) =>
        PublishAsync(client, topic, JsonSerializer.Serialize(payload), retain);

    private static Task PublishAsync(IManagedMqttClient client, string topic, string payload, bool retain) =>
        client.EnqueueAsync(new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag(retain)
            .Build());
}
