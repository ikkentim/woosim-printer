using System.Collections.Concurrent;

namespace ReceiptPrinter.Receipts;

/// <summary>
/// The bundled 72x72 monochrome weather glyphs (Material Design Icons - Pictogrammers Free License,
/// see Assets/Weather/README.md), handed back as centered <see cref="ImageElement"/>s keyed by Home
/// Assistant weather condition (the weather entity's state), e.g. "partlycloudy", "clear-night".
/// Unknown conditions just return <c>null</c> - the caller prints the text description without an icon.
/// </summary>
public static class WeatherIcon
{
    private static readonly ConcurrentDictionary<string, ImageElement?> Cache = new();
    private static readonly string[] ResourceNames = typeof(WeatherIcon).Assembly.GetManifestResourceNames();

    public static ImageElement? ForCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return null;

        var key = condition.Trim().ToLowerInvariant().Replace('-', '_');
        return Cache.GetOrAdd(key, Load);
    }

    private static ImageElement? Load(string key)
    {
        var name = ResourceNames.FirstOrDefault(n =>
            n.EndsWith($".Assets.Weather.{key}.pbm", StringComparison.Ordinal));
        if (name == null)
            return null;

        using var stream = typeof(WeatherIcon).Assembly.GetManifestResourceStream(name)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        return PbmImage.Parse(memory.ToArray(), Justification.Center);
    }
}
