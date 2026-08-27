namespace ReceiptPrinter;

public sealed class EnergyWidget : IBriefingWidget
{
    public async Task RenderAsync(IReceiptPrinter printer)
    {
        var energy = await LoadAsync();

        if (energy.ProducedKwh == null && energy.GridImportKwh == null && energy.GridExportKwh == null && energy.GasM3 == null)
            return;

        printer.SetBold(true);
        printer.Line("ENERGIE (gisteren)");
        printer.SetBold(false);
        if (energy.ProducedKwh != null)
            printer.Line($"- Geproduceerd: {energy.ProducedKwh:0.0} kWh");
        if (energy.GridImportKwh != null)
            printer.Line($"- Van net: {energy.GridImportKwh:0.0} kWh");
        if (energy.GridExportKwh != null)
            printer.Line($"- Teruggeleverd: {energy.GridExportKwh:0.0} kWh");
        if (energy.GasM3 != null)
            printer.Line($"- Gas: {energy.GasM3:0.0} m3");
    }

    private static async Task<EnergySummary> LoadAsync()
    {
        var haConfig = BriefingConfig.LoadHa();
        if (haConfig == null)
            return new EnergySummary(null, null, null, null);

        try
        {
            return await HomeAssistantEnergy.GetYesterdayAsync(haConfig.BaseUrl, haConfig.Token,
                haConfig.SolarProductionEntityId, haConfig.GridImportEntityIds, haConfig.GridExportEntityIds,
                haConfig.GasEntityId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Home Assistant energy fetch failed: {ex}");
            return new EnergySummary(null, null, null, null);
        }
    }
}
