using PrintTheDamnBook.Core.Text;

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

    public static Sheet[] Compute(IReadOnlyList<BookPage> pages, ReadingDirection direction = ReadingDirection.LeftToRight) => Impose(Pad(pages), direction);

    /// <summary>
    /// Lays out pages that are already padded to a multiple of 4 (see <see cref="Pad"/>). A right-to-left book opens from the right, so
    /// every face has its two pages swapped: front <c>left = p[2k]</c>, <c>right = p[N-1-2k]</c>; back <c>left = p[N-2-2k]</c>, <c>right = p[2k+1]</c>.
    /// </summary>
    public static Sheet[] Impose(IReadOnlyList<BookPage> p, ReadingDirection direction = ReadingDirection.LeftToRight)
    {
        var n = p.Count;
        if (n % PagesPerSheet != 0)
        {
            throw new ArgumentException($"Page count must be a multiple of {PagesPerSheet}; pad first.", nameof(p));
        }

        var sheets = Enumerable.Range(0, n / PagesPerSheet)
            .Select(k => new Sheet(
                k + 1,
                new SheetFace(Left: p[n - 1 - 2 * k], Right: p[2 * k]),
                new SheetFace(Left: p[2 * k + 1], Right: p[n - 2 - 2 * k])));
        return direction == ReadingDirection.RightToLeft
            ? sheets.Select(s => s with { Front = new SheetFace(s.Front.Right, s.Front.Left), Back = new SheetFace(s.Back.Right, s.Back.Left) }).ToArray()
            : sheets.ToArray();
    }

    public static bool IsTooThick(int sheetCount) => sheetCount > MaxFlatSheets;
}
