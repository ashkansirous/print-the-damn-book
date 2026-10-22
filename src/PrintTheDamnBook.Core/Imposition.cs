namespace PrintTheDamnBook.Core;

/// <summary>Saddle-stitch imposition: which page goes on which face of which sheet.</summary>
public static class Imposition
{
    public const int PagesPerSheet = 4;
    public const int MaxFlatSheets = 6;

    /// <summary>
    /// Pads <paramref name="pages"/> with blanks to a multiple of 4: before the back cover when the
    /// last page is one, otherwise at the end.
    /// </summary>
    public static IReadOnlyList<BookPage> Pad(IReadOnlyList<BookPage> pages)
    {
        var missing = (PagesPerSheet - pages.Count % PagesPerSheet) % PagesPerSheet;
        var blanks = Enumerable.Repeat(BookPage.Blank, missing);

        if (pages.Count == 0 || pages[^1].Kind != PageKind.BackCover)
        {
            return [.. pages, .. blanks];
        }

        return [.. pages.Take(pages.Count - 1), .. blanks, pages[^1]];
    }

    public static Sheet[] Compute(IReadOnlyList<BookPage> pages)
    {
        var p = Pad(pages);
        var n = p.Count;

        return Enumerable.Range(0, n / PagesPerSheet)
            .Select(k => new Sheet(
                k + 1,
                new SheetFace(Left: p[n - 1 - 2 * k], Right: p[2 * k]),
                new SheetFace(Left: p[2 * k + 1], Right: p[n - 2 - 2 * k])))
            .ToArray();
    }

    public static bool IsTooThick(int sheetCount) => sheetCount > MaxFlatSheets;
}
