using ReceiptPrinter.Receipts;
using ReceiptPrinter.Widgets;

namespace ReceiptPrinter.Service;

/// <summary>
/// Prints the daily briefing automatically once a day at a configured time.
/// </summary>
public sealed class BriefingScheduler : BackgroundService
{
    private readonly IReceiptPrinter _printer;
    private readonly TimeSpan _scheduledTime;
    private readonly ILogger<BriefingScheduler> _logger;

    public BriefingScheduler(IReceiptPrinter printer, IConfiguration config, ILogger<BriefingScheduler> logger)
    {
        _printer = printer;
        _logger = logger;

        // scheduled_hour/scheduled_minute come from the HA add-on's options.json when running as an
        // add-on (see Program.cs); Briefing:ScheduledHour/Minute is the plain appsettings.json fallback.
        var hour = config.GetValue<int?>("scheduled_hour") ?? config.GetValue("Briefing:ScheduledHour", 7);
        var minute = config.GetValue<int?>("scheduled_minute") ?? config.GetValue("Briefing:ScheduledMinute", 0);
        _scheduledTime = new TimeSpan(hour, minute, 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun();
            _logger.LogInformation("Next briefing scheduled in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);

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

    private TimeSpan TimeUntilNextRun()
    {
        var now = DateTime.Now;
        var next = now.Date + _scheduledTime;
        if (next <= now)
            next = next.AddDays(1);

        return next - now;
    }
}
