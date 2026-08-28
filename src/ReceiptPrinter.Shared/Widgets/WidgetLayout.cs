namespace ReceiptPrinter.Widgets;

/// <summary>Small fixed-width line helpers for the ~42-character receipt.</summary>
internal static class WidgetLayout
{
    public const int Width = 42;

    /// <summary>A full-width rule.</summary>
    public static string Divider() => new('-', Width);

    /// <summary>
    /// <paramref name="left"/> against the left margin and <paramref name="right"/> against the right,
    /// padded with spaces between. If they don't both fit, they're joined with a single space instead.
    /// </summary>
    public static string SplitLine(string left, string right)
    {
        var gap = Width - left.Length - right.Length;
        return gap >= 1 ? left + new string(' ', gap) + right : $"{left} {right}";
    }
}
