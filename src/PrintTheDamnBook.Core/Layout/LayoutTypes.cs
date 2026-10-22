namespace PrintTheDamnBook.Core.Layout;

/// <summary>A width and height in any single unit (millimetres in the paper maths, pixels for images).</summary>
public readonly record struct SizeD(double Width, double Height);

/// <summary>A rectangle in the same unit as the sizes it was computed from.</summary>
public readonly record struct RectD(double X, double Y, double Width, double Height);

public enum PaperKind
{
    A4,
    Letter,
}

/// <summary>Paper dimensions and unit conversion.</summary>
public static class Paper
{
    public const double MillimetresPerInch = 25.4;
    public const double DipsPerInch = 96;
    public const double DipsPerMillimetre = DipsPerInch / MillimetresPerInch;

    /// <summary>The sheet as it lies in the printer for a booklet: landscape, long edge horizontal.</summary>
    public static SizeD LandscapeSheetMm(PaperKind kind)
    {
        return kind switch
        {
            PaperKind.A4 => new SizeD(297, 210),
            PaperKind.Letter => new SizeD(11 * MillimetresPerInch, 8.5 * MillimetresPerInch),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>True when a paper size (in either orientation) is within the tolerance of the named paper's dimensions.</summary>
    public static bool MatchesSize(PaperKind kind, SizeD candidateMm, double toleranceMm)
    {
        var sheet = LandscapeSheetMm(kind);
        var longEdge = Math.Max(candidateMm.Width, candidateMm.Height);
        var shortEdge = Math.Min(candidateMm.Width, candidateMm.Height);
        return Math.Abs(longEdge - sheet.Width) <= toleranceMm && Math.Abs(shortEdge - sheet.Height) <= toleranceMm;
    }
}
