namespace PrintTheDamnBook.Core.Layout;

/// <summary>Whether page numbers are printed, and on which side of the page image.</summary>
public enum PageNumberPosition
{
    Off,
    Below,
    Above,
}

/// <summary>Which pages get which printed number, and the size of the number on the paper.</summary>
public static class PageNumbering
{
    /// <summary>Strip height reserved for the number on a sheet that is <see cref="ReferenceSheetHeightMm"/> tall.</summary>
    public const double ReferenceBandMm = 6;

    /// <summary>Em size of the number on a sheet that is <see cref="ReferenceSheetHeightMm"/> tall (about 10 pt).</summary>
    public const double ReferenceFontMm = 3.5;

    /// <summary>The height of an A4 landscape sheet; sizes scale with the sheet height relative to it.</summary>
    public const double ReferenceSheetHeightMm = 210;

    /// <summary>The number strip never comes closer than this to the paper edge, whatever the margin (printers cannot reach the very edge).</summary>
    public const double MinEdgeDistanceMm = 5;

    public static double BandMm(SizeD sheet)
    {
        return ReferenceBandMm * sheet.Height / ReferenceSheetHeightMm;
    }

    public static double FontMm(SizeD sheet)
    {
        return ReferenceFontMm * sheet.Height / ReferenceSheetHeightMm;
    }

    /// <summary>
    /// The number printed on each page of the book, keyed by page identity (reference). Numbers are the page's position
    /// in the padded book, so the first interior page is 1. Covers and blank pages get none but still count as a position
    /// (padding only sits before the back cover, so it never shifts an interior page).
    /// </summary>
    public static IReadOnlyDictionary<BookPage, int> Compute(IReadOnlyList<BookPage> padded)
    {
        var numbers = new Dictionary<BookPage, int>(ReferenceEqualityComparer.Instance);
        var firstIsCover = padded.Count > 0 && padded[0].Kind is PageKind.Cover or PageKind.DuplicateCover;
        var offset = firstIsCover ? 0 : 1;
        for (var i = 0; i < padded.Count; i++)
        {
            if (!IsUnnumbered(padded[i]))
            {
                numbers[padded[i]] = i + offset;
            }
        }

        return numbers;
    }

    private static bool IsUnnumbered(BookPage page)
    {
        return page.Kind is PageKind.Cover or PageKind.BackCover or PageKind.Blank or PageKind.Spacer or PageKind.DuplicateCover or PageKind.DuplicateBackCover;
    }
}
