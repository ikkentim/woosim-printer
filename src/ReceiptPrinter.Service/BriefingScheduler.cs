using Microsoft.Extensions.Options;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Widgets;

namespace ReceiptPrinter.Service;

/// <summary>
/// Prints the daily briefing automatically once a day at a configured time. Fully driven by
/// ReceiptPrinterOptions.Briefing via IOptionsMonitor, so toggling ScheduledBriefingEnabled or changing
/// ScheduledHour/Minute (e.g. from the Home Assistant add-on's Configuration tab) takes effect without
/// restarting the service.
/// </summary>
public sealed class BriefingScheduler(
    IReceiptPrinter printer,
    IOptionsMonitor<ReceiptPrinterOptions> options,
    ILogger<BriefingScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var briefing = options.CurrentValue.Briefing;
            var delay = TimeUntilNextRun(briefing.ScheduledHour, briefing.ScheduledMinute);
            logger.LogInformation("Next briefing check in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);

            // Re-read in case the schedule/enabled flag changed while we were waiting.
            var current = options.CurrentValue;
            if (!current.Briefing.ScheduledBriefingEnabled)
            {
                logger.LogInformation("Scheduled daily briefing is disabled, skipping");
                continue;
            }

            try
            {
                var receipt = await DailyBriefing.BuildAsync(current);
                await printer.PrintAsync(receipt);
                logger.LogInformation("Printed scheduled daily briefing");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to print scheduled daily briefing");
            }
        }
    }

    private static TimeSpan TimeUntilNextRun(int hour, int minute)
    {
        var now = DateTime.Now;
        var next = now.Date + new TimeSpan(hour, minute, 0);
        if (next <= now)
            next = next.AddDays(1);

        return next - now;
    }
}
