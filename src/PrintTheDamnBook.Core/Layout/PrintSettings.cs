using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrintTheDamnBook.Core.Layout;

/// <summary>What is remembered for one printer: its Two-sided choice and the manual-mode back options.</summary>
public sealed record PrinterSettings(DuplexMode Duplex = DuplexMode.Automatic, bool ReverseBacks = false, bool RotateBacks = ManualDuplexPlan.DefaultRotateBacks);

/// <summary>The options that stay set between runs. Every property has a usable default, so a partial file still loads.</summary>
public sealed record PrintSettings
{
    public ScalingMode Scaling { get; init; } = ScalingMode.Fit;

    public FlipEdge Flip { get; init; } = FlipEdge.ShortEdge;

    public double MarginMm { get; init; } = HalfSheetLayout.DefaultMarginMm;

    public PaperKind Paper { get; init; } = PaperKind.A4;

    public bool SeparateCoverJob { get; init; }

    public string? Printer { get; init; }

    /// <summary>Per printer name (case-insensitive).</summary>
    public Dictionary<string, PrinterSettings> Printers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public PrinterSettings ForPrinter(string name)
    {
        return Printers.GetValueOrDefault(name) ?? new PrinterSettings();
    }

    public PrintSettings WithPrinter(string name, PrinterSettings settings)
    {
        var printers = new Dictionary<string, PrinterSettings>(Printers, StringComparer.OrdinalIgnoreCase) { [name] = settings };
        return this with { Printers = printers };
    }
}

/// <summary>Reads and writes <see cref="PrintSettings"/> as JSON. Loading never throws; a missing or corrupt file gives the defaults.</summary>
public static class PrintSettingsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(PrintSettings settings)
    {
        return JsonSerializer.Serialize(settings, Options);
    }

    public static PrintSettings Parse(string? json)
    {
        try
        {
            var parsed = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PrintSettings>(json, Options);
            return parsed is null ? new PrintSettings() : Normalize(parsed);
        }
        catch (JsonException)
        {
            return new PrintSettings();
        }
    }

    private static bool IsFileFailure(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException;
    }

    public static PrintSettings Load(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            return new PrintSettings();
        }
    }

    /// <summary>Writes through a unique temporary file so a crash never leaves half a file. Returns false when the file could not be written.</summary>
    public static bool TrySave(string path, PrintSettings settings)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, Serialize(settings));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            TryDelete(temp);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            // Nothing more to do: the stray temp file is harmless.
        }
    }

    /// <summary>Repairs values that parsed but make no sense (out-of-range enums, margin, null entries).</summary>
    private static PrintSettings Normalize(PrintSettings settings)
    {
        var printers = new Dictionary<string, PrinterSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in settings.Printers ?? [])
        {
            if (value is not null && Enum.IsDefined(value.Duplex))
            {
                printers[name] = value;
            }
        }

        return settings with
        {
            Scaling = Enum.IsDefined(settings.Scaling) ? settings.Scaling : ScalingMode.Fit,
            Flip = Enum.IsDefined(settings.Flip) ? settings.Flip : FlipEdge.ShortEdge,
            Paper = Enum.IsDefined(settings.Paper) ? settings.Paper : PaperKind.A4,
            MarginMm = HalfSheetLayout.NormalizeMargin(settings.MarginMm),
            Printers = printers,
        };
    }
}
