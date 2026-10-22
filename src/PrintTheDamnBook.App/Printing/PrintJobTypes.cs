using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Printing;

internal enum PrintPart
{
    Booklet,
    Cover,
    Rest,
    TestSheet,
}

internal enum PrintOutcome
{
    Sent,

    /// <summary>Known to have failed before any job reached the spooler.</summary>
    NothingSent,

    /// <summary>Failed after the spooler took the job, or in a way that does not say; some pages may have printed.</summary>
    MayBePartial,

    /// <summary>The fronts were printed and the backs were not (cancelled or failed).</summary>
    FrontsOnly,
}

internal sealed record PartText(string Phrase, string JobSuffix);

internal sealed record PrintTarget(string PrinterName, PaperKind Paper, JobLayout Layout, string BookName);

/// <summary>What the manual two-pass flow reads from, and reports to, the print view model.</summary>
internal interface IManualDuplexSession
{
    bool ReverseBacks { get; }

    bool RotateBacks { get; set; }

    string? PrintStatus { get; set; }
}

internal static class PrintParts
{
    private static readonly IReadOnlyDictionary<PrintPart, PartText> Texts = new Dictionary<PrintPart, PartText>
    {
        [PrintPart.Booklet] = new("the booklet", string.Empty),
        [PrintPart.Cover] = new("the cover sheet", " - cover sheet"),
        [PrintPart.Rest] = new("the rest of the booklet", " - inside pages"),
        [PrintPart.TestSheet] = new("the test sheet", " - test sheet"),
    };

    public static PartText Text(PrintPart part)
    {
        return Texts[part];
    }
}

/// <summary>Turns a print exception into text for the user, saying only what is known about what was printed.</summary>
internal static class PrintErrors
{
    private const string NothingPrinted = " Nothing was printed.";
    private const string MayHavePartlyPrinted = " Some pages may have printed; check the printer queue.";
    private const string NoBacksPrinted = " No backs were printed.";
    private const string SomeBacksMayHavePrinted = " Some backs may have printed; check the printer queue.";

    public static string Describe(Exception ex, string printerName)
    {
        return ex is PrintFailedException ? ex.Message : $"Printing to {printerName} failed: {ex.Message}";
    }

    /// <summary>The whole-job failure text: "Nothing was printed." only when no job reached the spooler.</summary>
    public static string JobFailure(Exception ex, string printerName)
    {
        return Describe(ex, printerName) + (IsNothingSent(ex) ? NothingPrinted : MayHavePartlyPrinted);
    }

    public static string BacksFailure(Exception ex, string printerName)
    {
        return Describe(ex, printerName) + (IsNothingSent(ex) ? NoBacksPrinted : SomeBacksMayHavePrinted);
    }

    public static PrintOutcome OutcomeOf(Exception ex)
    {
        return IsNothingSent(ex) ? PrintOutcome.NothingSent : PrintOutcome.MayBePartial;
    }

    private static bool IsNothingSent(Exception ex)
    {
        return ex is PrintFailedException { MayHavePrinted: false };
    }
}
