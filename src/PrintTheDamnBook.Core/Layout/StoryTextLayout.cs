using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Layout;

/// <summary>The panel over a picture: where it may sit (<see cref="MaxPanel"/>), the area its words may use, and its corner radius. All in the unit of the rectangle it was computed from (millimetres on the sheet).</summary>
public readonly record struct BandArea(RectD MaxPanel, RectD Text, double CornerRadius, double PaddingY);

/// <summary>The words of a text page: the whole page (the paper the tint fills) and the box the words are set in.</summary>
public readonly record struct TextPageArea(RectD Page, RectD Text);

/// <summary>The size chosen for some words: the point size, whether they fit at it, and whether it is smaller than the first step.</summary>
public readonly record struct TextFitResult(double Pt, bool Fits, bool Shrunk);

/// <summary>
/// Where story text goes and how big it is. The panel over a picture takes Dino Reader's values (a 92% white panel, ink
/// <c>#2B2540</c>, margin 8, corner 16, padding 14 x 10 on a 360-wide card), scaled with the picture's width, and is never
/// taller than 30% of the picture. Sizes shrink in fixed steps until the words fit.
/// </summary>
public static class StoryTextLayout
{
    /// <summary>The panel and the margin under it together are at most this part of the visible picture's height.</summary>
    public const double MaxBandFraction = 0.30;

    /// <summary>Dino Reader's text card opacity.</summary>
    public const double PanelOpacity = 0.92;

    public static readonly Rgb PanelFill = new(0xFF, 0xFF, 0xFF);

    public static readonly Rgb Ink = new(0x2B, 0x25, 0x40);

    public const double BandLineHeight = 1.5;
    public const double PageLineHeight = 1.35;

    /// <summary>Dino Reader's values are in units of a card this wide; they scale with the picture's width.</summary>
    private const double ReferenceCardWidth = 360;

    private const double PanelMarginUnits = 8;
    private const double PanelCornerUnits = 16;
    private const double PanelPaddingXUnits = 14;
    private const double PanelPaddingYUnits = 10;

    /// <summary>The words on a text page keep this part of the page's width free on every side.</summary>
    public const double TextPageInsetFraction = 0.12;

    public const double MillimetresPerPoint = Paper.MillimetresPerInch / 72;

    private static readonly double[] BandStepsA4 = [22, 20, 18, 16, 14];
    private static readonly double[] PageStepsA4 = [28, 24, 20, 18, 16];

    /// <summary>The sizes to try for words over a picture, largest first; the first is the normal size.</summary>
    public static IReadOnlyList<double> BandStepsPt(PaperKind paper) => paper switch
    {
        PaperKind.A4 or PaperKind.Letter => BandStepsA4,
        _ => throw new ArgumentOutOfRangeException(nameof(paper), paper, null),
    };

    /// <summary>The sizes to try for words on a page of their own, largest first.</summary>
    public static IReadOnlyList<double> PageStepsPt(PaperKind paper) => paper switch
    {
        PaperKind.A4 or PaperKind.Letter => PageStepsA4,
        _ => throw new ArgumentOutOfRangeException(nameof(paper), paper, null),
    };

    public static double PtToMm(double pt) => pt * MillimetresPerPoint;

    /// <summary>
    /// The first size (steps are largest first) for which <paramref name="fitsAt"/> is true. When none fits, the smallest
    /// step is returned with <see cref="TextFitResult.Fits"/> false.
    /// </summary>
    public static TextFitResult Choose(IReadOnlyList<double> stepsPt, Func<double, bool> fitsAt)
    {
        if (stepsPt.Count == 0)
        {
            throw new ArgumentException("At least one size is needed.", nameof(stepsPt));
        }

        foreach (var pt in stepsPt)
        {
            if (fitsAt(pt))
            {
                return new TextFitResult(pt, true, pt < stepsPt[0]);
            }
        }

        return new TextFitResult(stepsPt[^1], false, stepsPt.Count > 1);
    }

    /// <summary>The panel over the bottom of the visible part of a picture.</summary>
    public static BandArea Band(RectD visible)
    {
        var unit = visible.Width / ReferenceCardWidth;
        var margin = PanelMarginUnits * unit;
        var paddingX = PanelPaddingXUnits * unit;
        var paddingY = PanelPaddingYUnits * unit;
        var height = MaxBandFraction * visible.Height - margin;
        var panel = new RectD(visible.X + margin, visible.Y + visible.Height - margin - height, visible.Width - 2 * margin, height);
        var text = new RectD(panel.X + paddingX, panel.Y + paddingY, panel.Width - 2 * paddingX, panel.Height - 2 * paddingY);
        return new BandArea(panel, text, PanelCornerUnits * unit, paddingY);
    }

    /// <summary>The panel rectangle for words that are <paramref name="textHeight"/> tall: as tall as they need, bottom-aligned, never above the maximum.</summary>
    public static RectD Panel(BandArea band, double textHeight)
    {
        var height = Math.Min(band.MaxPanel.Height, textHeight + 2 * band.PaddingY);
        return band.MaxPanel with { Y = band.MaxPanel.Y + band.MaxPanel.Height - height, Height = height };
    }

    /// <summary>The text page on the left or right half of the sheet: the half itself, and the box for the words inside the margin and number strip.</summary>
    public static TextPageArea TextPage(SizeD sheet, bool rightHalf, double margin, PageNumberPosition numbers)
    {
        var half = HalfSheetLayout.HalfOf(sheet);
        var offset = rightHalf ? half.Width : 0;
        var available = HalfSheetLayout.Available(half, margin, numbers, PageNumbering.BandMm(sheet));
        var inset = TextPageInsetFraction * half.Width;
        var text = new RectD(available.X + offset + inset, available.Y + inset, available.Width - 2 * inset, available.Height - 2 * inset);
        return new TextPageArea(new RectD(offset, 0, half.Width, half.Height), text);
    }

    /// <summary>The top of words <paramref name="textHeight"/> tall, centred vertically in <paramref name="box"/> (never above it).</summary>
    public static double CentredTop(RectD box, double textHeight) => box.Y + Math.Max(0, (box.Height - textHeight) / 2);
}
