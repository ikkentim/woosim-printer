using System.Text;

namespace ReceiptPrinter.Receipts;

/// <summary>
/// A tiny, receipt-specific markdown dialect for freeform print requests (the MQTT `notify` entity,
/// primarily) - lets an automation's message string carry basic formatting without needing to build a
/// full Receipt JSON payload by hand.
///
/// Per line:
/// - A line that's just "~~~" (nothing else, whitespace aside) requests a full cut instead of the
///   default partial one - consumes the line itself, prints nothing for it. Escape as "\~~~" to print
///   the literal text instead.
/// - A line that's just "[WidgetName]" (e.g. "[Weather]") splices in that briefing widget's own output
///   in place of the line - see <see cref="Widgets.DailyBriefingWidget.CreateWidgetFactories"/> for the
///   available names. Unknown names are skipped (nothing printed for that line).
/// - A line that's just "![name]" (e.g. "![sunny]") prints that bundled weather glyph, centered - see
///   <see cref="WeatherIcon.ForCondition"/> (names are the Home Assistant weather conditions:
///   "sunny", "partlycloudy", "rainy", "clear-night", ...). Unknown names are skipped.
/// - A line starting with ">>" right-justifies; ">" centers; otherwise left (default) - any whitespace
///   right after the marker is trimmed. Checked before the heading marker, so e.g. "> # Heading" is a
///   centered heading.
/// - A line starting with "# " prints big (bold, double width/height) - the rest of that line is still
///   parsed for inline markers below.
/// - "**bold**" and "*underline*" toggle bold/underline for the enclosed text - they can be mixed and
///   nested with each other, and each toggle can appear multiple times per line.
/// - "\*", "\#", "\>" print a literal *, #, > respectively; any other "\x" prints just "x".
///
/// Not real Markdown (no links, lists, etc.) - just enough to make a printed note look intentional.
/// </summary>
public static class ReceiptMarkdown
{
    /// <param name="resolveWidget">Resolves a "[WidgetName]" line to that widget's rendered elements -
    /// omit to just skip such lines. Kept as a delegate (rather than this class taking a
    /// ReceiptPrinterOptions directly) so this stays a plain text-formatting utility with no dependency
    /// on how widgets are constructed.</param>
    public static async Task<Receipt> ParseAsync(string text, Func<string, Task<IReadOnlyList<IElement>>>? resolveWidget = null)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var elements = new List<IElement>();
        var cut = CutStyle.Partial;

        foreach (var line in lines)
        {
            if (line.Trim() == "~~~")
            {
                cut = CutStyle.Full;
                continue;
            }

            var iconName = TryGetImageReference(line);
            if (iconName != null)
            {
                if (WeatherIcon.ForCondition(iconName) is { } icon)
                    elements.Add(icon);
                continue;
            }

            var widgetName = TryGetWidgetReference(line);
            if (widgetName != null)
            {
                if (resolveWidget != null)
                    elements.AddRange(await resolveWidget(widgetName));
                continue;
            }

            elements.AddRange(ParseLine(line));
        }

        return new Receipt(elements, cut);
    }

    private static string? TryGetWidgetReference(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 3 && trimmed[0] == '[' && trimmed[^1] == ']' ? trimmed[1..^1] : null;
    }

    private static string? TryGetImageReference(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 4 && trimmed[0] == '!' && trimmed[1] == '[' && trimmed[^1] == ']'
            ? trimmed[2..^1]
            : null;
    }

    private static IEnumerable<IElement> ParseLine(string line)
    {
        var justification = Justification.Left;
        if (line.StartsWith(">>", StringComparison.Ordinal))
        {
            justification = Justification.Right;
            line = line[2..].TrimStart();
        }
        else if (line.StartsWith(">", StringComparison.Ordinal))
        {
            justification = Justification.Center;
            line = line[1..].TrimStart();
        }

        var heading = line.Length >= 2 && line[0] == '#' && line[1] == ' ';
        if (heading)
            line = line[2..];

        var (width, height) = heading ? (2, 2) : (1, 1);

        var result = new List<IElement>();
        var run = new StringBuilder();
        var bold = heading;
        var underline = false;

        void Flush(bool lineBreak)
        {
            if (run.Length == 0 && !lineBreak)
                return;

            result.Add(new TextElement(run.ToString(), LineBreak: lineBreak, Bold: bold, Underline: underline, Width: width, Height: height, Justification: justification));
            run.Clear();
        }

        var i = 0;
        while (i < line.Length)
        {
            var c = line[i];

            if (c == '\\' && i + 1 < line.Length)
            {
                run.Append(line[i + 1]);
                i += 2;
                continue;
            }

            if (c == '*' && i + 1 < line.Length && line[i + 1] == '*')
            {
                Flush(lineBreak: false);
                bold = !bold;
                i += 2;
                continue;
            }

            if (c == '*')
            {
                Flush(lineBreak: false);
                underline = !underline;
                i += 1;
                continue;
            }

            run.Append(c);
            i += 1;
        }

        // Always emitted, even if empty - this is what actually terminates the line with a newline,
        // regardless of whether the line ended with an unclosed run.
        Flush(lineBreak: true);

        return result;
    }
}
