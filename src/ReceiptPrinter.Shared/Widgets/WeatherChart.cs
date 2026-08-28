using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// Rasterises a row of values into a simple filled bar-chart bitmap in-process (no imaging library),
/// for printing via <see cref="ImageElement"/>. Bars run from 0 up to each value, spread across the
/// full requested width, over a 1px baseline axis.
/// </summary>
internal static class WeatherChart
{
    public static ImageElement Bars(IReadOnlyList<double> values, int width, int height)
    {
        var stride = (width + 7) / 8;
        var rows = new byte[height * stride];

        void Set(int x, int y)
        {
            if ((uint)x < (uint)width && (uint)y < (uint)height)
                rows[y * stride + (x >> 3)] |= (byte)(0x80 >> (x & 7));
        }

        for (var x = 0; x < width; x++)
            Set(x, height - 1); // baseline

        var max = Math.Max(values.DefaultIfEmpty(0).Max(), 0.001);
        var plotHeight = height - 2;

        for (var i = 0; i < values.Count; i++)
        {
            var x0 = (int)((long)i * width / values.Count);
            var x1 = (int)((long)(i + 1) * width / values.Count);
            var barHeight = Math.Clamp((int)Math.Round(values[i] / max * plotHeight), 0, plotHeight);

            for (var x = x0; x < x1; x++)
                for (var y = height - 1 - barHeight; y < height - 1; y++)
                    Set(x, y);
        }

        // Left-aligned so it shares an origin with the label TextElements around it.
        return new ImageElement(rows, width, height, Justification.Left);
    }
}
