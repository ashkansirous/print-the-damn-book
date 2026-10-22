using System.Globalization;

namespace PrintTheDamnBook.Core;

/// <summary>Orders the image files of a folder into book page order.</summary>
public static class PageOrdering
{
    private const string CoverStem = "cover";
    private const string BackCoverStem = "back cover";

    public static IReadOnlyList<BookPage> FromFolder(IEnumerable<string> fileNames)
    {
        var cover = new List<BookPage>();
        var numbered = new List<(long Number, BookPage Page)>();
        var other = new List<BookPage>();
        var backCover = new List<BookPage>();

        foreach (var fileName in fileNames)
        {
            Classify(fileName, cover, numbered, other, backCover);
        }

        var coverPages = TakeSingle(cover, other, PageKind.DuplicateCover);
        var backCoverPages = TakeSingle(backCover, other, PageKind.DuplicateBackCover);

        return
        [
            .. coverPages,
            .. numbered.OrderBy(n => n.Number).ThenBy(n => n.Page.Path, StringComparer.OrdinalIgnoreCase).Select(n => n.Page),
            .. other.OrderBy(p => p.Label, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase),
            .. backCoverPages,
        ];
    }

    /// <summary>Keeps the first candidate (by path) and demotes duplicates, e.g. Cover.jpg plus Cover.png, labelled by file name.</summary>
    private static List<BookPage> TakeSingle(List<BookPage> candidates, List<BookPage> other, PageKind duplicateKind)
    {
        var ordered = candidates.OrderBy(p => p.Path, StringComparer.OrdinalIgnoreCase).ToList();
        other.AddRange(ordered.Skip(1).Select(p => p with { Kind = duplicateKind, Label = Path.GetFileName(p.Path)! }));
        return ordered.Take(1).ToList();
    }

    private static void Classify(
        string fileName,
        List<BookPage> cover,
        List<(long Number, BookPage Page)> numbered,
        List<BookPage> other,
        List<BookPage> backCover)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);

        if (stem.Equals(BackCoverStem, StringComparison.OrdinalIgnoreCase))
        {
            backCover.Add(new BookPage(fileName, stem, PageKind.BackCover));
        }
        else if (stem.Equals(CoverStem, StringComparison.OrdinalIgnoreCase))
        {
            cover.Add(new BookPage(fileName, stem, PageKind.Cover));
        }
        else if (long.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            numbered.Add((number, new BookPage(fileName, stem, PageKind.Numbered)));
        }
        else
        {
            other.Add(new BookPage(fileName, stem, PageKind.Other));
        }
    }
}
