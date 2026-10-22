namespace PrintTheDamnBook.Core.Layout;

/// <summary>Where a page image sits on a landscape sheet that holds two pages side by side.</summary>
public static class HalfSheetLayout
{
    public const double DefaultMarginMm = 5;

    /// <summary>
    /// The rectangle for an image scaled to fit inside a half sheet (less the margin on every side)
    /// with its aspect kept and centred, relative to the half's top-left corner.
    /// </summary>
    public static RectD Fit(SizeD image, SizeD halfSheet, double margin)
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

        var scale = Math.Min(availableWidth / image.Width, availableHeight / image.Height);
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
        var half = HalfOf(sheet);
        var fitted = Fit(image, half, margin);
        var offset = rightHalf ? half.Width : 0;
        return fitted with { X = fitted.X + offset };
    }
}
