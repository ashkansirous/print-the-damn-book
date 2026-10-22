namespace PrintTheDamnBook.Core.Layout;

/// <summary>How a page image is scaled into its half of the sheet.</summary>
public enum ScalingMode
{
    /// <summary>Whole page visible, white bars where the aspect differs.</summary>
    Fit,

    /// <summary>Page covers its half and is cropped equally on the overflowing axis.</summary>
    Fill,
}

/// <summary>Which edge of the sheet the printer's duplex unit flips about.</summary>
public enum FlipEdge
{
    ShortEdge,
    LongEdge,
}

/// <summary>Whether the printer does the two-sided printing or the user flips the stack.</summary>
public enum DuplexMode
{
    Automatic,
    Manual,
}

/// <summary>Where the page numbers go and which number each page gets (see <see cref="PageNumbering.Compute"/>). The two always travel together.</summary>
public sealed record PageNumberSetup(PageNumberPosition Position, IReadOnlyDictionary<BookPage, int> Numbers);

/// <summary>The layout choices that apply to a whole print job. A null <paramref name="PageNumbers"/> prints no numbers.</summary>
public sealed record JobLayout(
    ScalingMode Scaling,
    double MarginMm,
    FlipEdge Flip,
    PageNumberSetup? PageNumbers = null)
{
    public PageNumberPosition NumberPosition => PageNumbers?.Position ?? PageNumberPosition.Off;
}

/// <summary>The size of one page box in the sheet preview and the inset the margin leaves inside it, in the same unit.</summary>
public readonly record struct PreviewBox(double Width, double Height, double Inset);

/// <summary>A page image's scaled rectangle and the part of it that shows, both in sheet coordinates.</summary>
public readonly record struct PagePlacement(RectD Image, RectD Visible)
{
    /// <summary>The visible part relative to the image's own top-left corner, for clipping the drawn image.</summary>
    public RectD LocalClip => new(Visible.X - Image.X, Visible.Y - Image.Y, Visible.Width, Visible.Height);

    /// <summary>The placement after the whole sheet is turned 180 degrees about its centre.</summary>
    public PagePlacement Rotate180(SizeD sheet)
    {
        return new PagePlacement(HalfSheetLayout.Rotate180(Image, sheet), HalfSheetLayout.Rotate180(Visible, sheet));
    }
}
