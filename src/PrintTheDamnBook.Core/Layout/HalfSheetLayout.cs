using System.Globalization;

namespace PrintTheDamnBook.Core.Layout;

/// <summary>Where a page image sits on a landscape sheet that holds two pages side by side.</summary>
public static class HalfSheetLayout
{
    public const double DefaultMarginMm = 5;
    public const double MaxMarginMm = 25;

    /// <summary>A usable margin: NaN becomes the default, negative becomes 0, anything above <see cref="MaxMarginMm"/> is capped.</summary>
    public static double NormalizeMargin(double marginMm)
    {
        return double.IsNaN(marginMm) ? DefaultMarginMm : Math.Clamp(marginMm, 0, MaxMarginMm);
    }

    /// <summary>
    /// The rectangle for an image scaled to fit inside a half sheet (less the margin on every side)
    /// with its aspect kept and centred, relative to the half's top-left corner.
    /// </summary>
    public static RectD Fit(SizeD image, SizeD halfSheet, double margin)
    {
        return Scale(image, Available(halfSheet, margin), Math.Min);
    }

    /// <summary>
    /// The rectangle for an image scaled to cover the half sheet less the margin, centred, relative to the half's
    /// top-left corner. It overflows the available area on one axis; the overflow is cropped equally on both sides.
    /// </summary>
    public static RectD Fill(SizeD image, SizeD halfSheet, double margin)
    {
        return Scale(image, Available(halfSheet, margin), Math.Max);
    }

    /// <summary>The area inside the margin of a half sheet, relative to the half's top-left corner.</summary>
    public static RectD Available(SizeD halfSheet, double margin)
    {
        return new RectD(margin, margin, halfSheet.Width - 2 * margin, halfSheet.Height - 2 * margin);
    }

    /// <summary>
    /// The area inside the margin that the page image may use, less the strip kept for the page number
    /// (on the bottom for Below, on the top for Above).
    /// </summary>
    public static RectD Available(SizeD halfSheet, double margin, PageNumberPosition numbers, double numberBand)
    {
        return Split(halfSheet, margin, numbers, numberBand).Image;
    }

    /// <summary>
    /// Splits the half sheet into the image area and the number strip. The strip keeps at least
    /// <see cref="PageNumbering.MinEdgeDistanceMm"/> from the paper edge whatever the margin, and the image area is
    /// what is left, so the two can never overlap.
    /// </summary>
    private static (RectD Image, RectD? Strip) Split(SizeD half, double margin, PageNumberPosition numbers, double band)
    {
        var area = Available(half, margin);
        var edge = Math.Max(margin, PageNumbering.MinEdgeDistanceMm);
        return numbers switch
        {
            PageNumberPosition.Below => BelowSplit(area, half, edge, band),
            PageNumberPosition.Above => AboveSplit(area, half, edge, band),
            _ => (area, null),
        };
    }

    private static (RectD Image, RectD? Strip) BelowSplit(RectD area, SizeD half, double edge, double band)
    {
        var imageBottom = half.Height - edge - band;
        var image = area with { Height = imageBottom - area.Y };
        return (image, new RectD(0, imageBottom, half.Width, band));
    }

    private static (RectD Image, RectD? Strip) AboveSplit(RectD area, SizeD half, double edge, double band)
    {
        var imageTop = edge + band;
        var image = area with { Y = imageTop, Height = area.Y + area.Height - imageTop };
        return (image, new RectD(0, edge, half.Width, band));
    }

    private static RectD Scale(SizeD image, RectD area, Func<double, double, double> pick)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(image), image, "Image size must be positive.");
        }

        if (area.X < 0 || area.Width <= 0 || area.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(area), area, "Margin leaves no room for the page.");
        }

        var scale = pick(area.Width / image.Width, area.Height / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        return new RectD(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
    }

    public static SizeD HalfOf(SizeD sheet)
    {
        return new SizeD(sheet.Width / 2, sheet.Height);
    }

    /// <summary>The fitted rectangle in sheet coordinates for the left or right half.</summary>
    public static RectD Place(SizeD image, SizeD sheet, bool rightHalf, double margin)
    {
        return Resolve(image, sheet, rightHalf, margin, ScalingMode.Fit).Image;
    }

    /// <summary>
    /// The page's scaled rectangle and visible part in sheet coordinates for the left or right half. With a page
    /// number the image is fitted into the area left after the number's strip.
    /// </summary>
    public static PagePlacement Resolve(SizeD image, SizeD sheet, bool rightHalf, double margin, ScalingMode mode, PageNumberPosition numbers = PageNumberPosition.Off)
    {
        var half = HalfOf(sheet);
        var offset = rightHalf ? half.Width : 0;
        var available = Available(half, margin, numbers, PageNumbering.BandMm(sheet));
        var scaled = Scale(image, available, mode == ScalingMode.Fit ? Math.Min : Math.Max);
        var visible = mode == ScalingMode.Fit ? scaled : available;
        return new PagePlacement(scaled with { X = scaled.X + offset }, visible with { X = visible.X + offset });
    }

    /// <summary>
    /// The strip that holds a page number, in sheet coordinates for the left or right half; null when numbers are off.
    /// It is full half width, directly beside the image area, and never closer to the paper edge than
    /// <see cref="PageNumbering.MinEdgeDistanceMm"/>.
    /// </summary>
    public static RectD? PageNumberRect(SizeD sheet, bool rightHalf, double margin, PageNumberPosition numbers)
    {
        var half = HalfOf(sheet);
        if (Split(half, margin, numbers, PageNumbering.BandMm(sheet)).Strip is not { } strip)
        {
            return null;
        }

        return strip with { X = rightHalf ? half.Width : 0 };
    }

    /// <summary>
    /// The on-screen box for one page of the sheet preview: the real half-sheet aspect at <paramref name="boxWidth"/>,
    /// with the margin scaled to match, so a Fill crop in the preview matches the print.
    /// </summary>
    public static PreviewBox Preview(SizeD sheet, double marginMm, double boxWidth)
    {
        var half = HalfOf(sheet);
        var scale = boxWidth / half.Width;
        return new PreviewBox(boxWidth, half.Height * scale, NormalizeMargin(marginMm) * scale);
    }

    /// <summary>
    /// Reads a margin typed by the user. Margins are small mm values with no thousands separator, so '.' and ','
    /// both mean the decimal point whatever the culture; more than one separator is rejected.
    /// </summary>
    public static bool TryParseMargin(string? text, out double marginMm)
    {
        marginMm = 0;
        var normalised = text?.Trim().Replace(',', '.');
        if (string.IsNullOrEmpty(normalised) || normalised.Count(c => c == '.') > 1)
        {
            return false;
        }

        return double.TryParse(normalised, NumberStyles.Float, CultureInfo.InvariantCulture, out marginMm);
    }

    public static string FormatMargin(double marginMm, IFormatProvider culture)
    {
        return NormalizeMargin(marginMm).ToString("0.##", culture);
    }

    /// <summary>The rectangle after the whole sheet is turned 180 degrees about its centre (a point reflection).</summary>
    public static RectD Rotate180(RectD rect, SizeD sheet)
    {
        return new RectD(sheet.Width - rect.X - rect.Width, sheet.Height - rect.Y - rect.Height, rect.Width, rect.Height);
    }
}
