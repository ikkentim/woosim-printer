using ReceiptPrinter.Configuration;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Widgets;

namespace ReceiptPrinter.Service;

/// <summary>
/// Prints the daily briefing automatically once a day at a configured time. Fully driven by
/// briefing-settings.json (see BriefingConfig.LoadSettings) - re-read on every iteration, so toggling
/// ScheduledBriefingEnabled or changing ScheduledHour/Minute takes effect without restarting the service.
/// </summary>
public sealed class BriefingScheduler : BackgroundService
{
    private readonly IReceiptPrinter _printer;
    private readonly ILogger<BriefingScheduler> _logger;

    public BriefingScheduler(IReceiptPrinter printer, ILogger<BriefingScheduler> logger)
    {
        _printer = printer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = BriefingConfig.LoadSettings();
            var delay = TimeUntilNextRun(settings.ScheduledHour, settings.ScheduledMinute);
            _logger.LogInformation("Next briefing check in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);

            // Re-read in case the schedule/enabled flag changed while we were waiting.
            settings = BriefingConfig.LoadSettings();
            if (!settings.ScheduledBriefingEnabled)
            {
                _logger.LogInformation("Scheduled daily briefing is disabled, skipping");
                continue;
            }

            try
            {
                var receipt = await DailyBriefing.BuildAsync();
                await _printer.PrintAsync(receipt);
                _logger.LogInformation("Printed scheduled daily briefing");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to print scheduled daily briefing");
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
