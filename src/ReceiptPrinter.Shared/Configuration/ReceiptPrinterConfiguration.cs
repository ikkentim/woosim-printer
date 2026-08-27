using Microsoft.Extensions.Configuration;

namespace ReceiptPrinter.Configuration;

/// <summary>
/// Builds the IConfiguration all of this app's settings come from - the same layering for the CLI and
/// the Service, so there's exactly one config model instead of a pile of hand-rolled JSON-file loaders:
///
///   1. appsettings.json (committed, safe non-secret defaults)
///   2. appsettings.local.json next to it, or under RECEIPTPRINTER_CONFIG_DIR (git-ignored, for secrets
///      when running outside Home Assistant)
///   3. /data/options.json (Home Assistant add-on options, written by Supervisor - reloads live)
///   4. environment variables (e.g. HomeAssistant__Token=..., double underscore for nesting)
///
/// Later sources override earlier ones.
/// </summary>
public static class ReceiptPrinterConfiguration
{
    public static IConfigurationRoot Build()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(ConfigPaths.Combine("appsettings.local.json"), optional: true)
            .AddJsonFile("/data/options.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    public static ReceiptPrinterOptions Load(IConfiguration? configuration = null)
    {
        var options = new ReceiptPrinterOptions();
        (configuration ?? Build()).Bind(options);
        return options;
    }
}
