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
        return Scale(image, halfSheet, margin, Math.Min);
    }

    /// <summary>
    /// The rectangle for an image scaled to cover the half sheet less the margin, centred, relative to the half's
    /// top-left corner. It overflows the available area on one axis; the overflow is cropped equally on both sides.
    /// </summary>
    public static RectD Fill(SizeD image, SizeD halfSheet, double margin)
    {
        return Scale(image, halfSheet, margin, Math.Max);
    }

    /// <summary>The area inside the margin of a half sheet, relative to the half's top-left corner.</summary>
    public static RectD Available(SizeD halfSheet, double margin)
    {
        return new RectD(margin, margin, halfSheet.Width - 2 * margin, halfSheet.Height - 2 * margin);
    }

    private static RectD Scale(SizeD image, SizeD halfSheet, double margin, Func<double, double, double> pick)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(image), image, "Image size must be positive.");
        }

        var availableWidth = halfSheet.Width - 2 * margin;
        var availableHeight = halfSheet.Height - 2 * margin;
        if (margin < 0 || availableWidth <= 0 || availableHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(margin), margin, "Margin leaves no room for the page.");
        }

        var scale = pick(availableWidth / image.Width, availableHeight / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        return new RectD((halfSheet.Width - width) / 2, (halfSheet.Height - height) / 2, width, height);
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

    /// <summary>The page's scaled rectangle and visible part in sheet coordinates for the left or right half.</summary>
    public static PagePlacement Resolve(SizeD image, SizeD sheet, bool rightHalf, double margin, ScalingMode mode)
    {
        var half = HalfOf(sheet);
        var offset = rightHalf ? half.Width : 0;
        if (mode == ScalingMode.Fit)
        {
            var fitted = Fit(image, half, margin);
            fitted = fitted with { X = fitted.X + offset };
            return new PagePlacement(fitted, fitted);
        }

        var filled = Fill(image, half, margin);
        var available = Available(half, margin);
        return new PagePlacement(filled with { X = filled.X + offset }, available with { X = available.X + offset });
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
