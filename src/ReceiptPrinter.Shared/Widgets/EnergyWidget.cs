using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

public sealed class EnergyWidget : IBriefingWidget
{
    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var energy = await LoadAsync();

        if (energy.ProducedKwh == null && energy.GridImportKwh == null && energy.GridExportKwh == null && energy.GasM3 == null)
            return Array.Empty<IElement>();

        var elements = new List<IElement> { new TextElement(Localization.T("energy.heading"), Bold: true) };

        if (energy.ProducedKwh != null)
            elements.Add(new TextElement($"- {Localization.T("energy.produced")}: {energy.ProducedKwh:0.0} kWh"));
        if (energy.GridImportKwh != null)
            elements.Add(new TextElement($"- {Localization.T("energy.grid_import")}: {energy.GridImportKwh:0.0} kWh"));
        if (energy.GridExportKwh != null)
            elements.Add(new TextElement($"- {Localization.T("energy.grid_export")}: {energy.GridExportKwh:0.0} kWh"));
        if (energy.GasM3 != null)
            elements.Add(new TextElement($"- {Localization.T("energy.gas")}: {energy.GasM3:0.0} m3"));

        return elements;
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
