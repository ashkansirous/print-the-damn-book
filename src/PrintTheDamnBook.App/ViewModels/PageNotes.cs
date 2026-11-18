using PrintTheDamnBook.Core;

namespace PrintTheDamnBook.App.ViewModels;

/// <summary>The note under a page tile and whether it is a problem (warning ring) or only a hint.</summary>
internal static class PageNotes
{
    private const string Unrecognised = "Name not recognised, so it went to the end.";
    private const string DuplicateCover = "There is already a Cover, so this went to the end.";
    private const string DuplicateBackCover = "There is already a Back cover, so this went to the end.";
    private const string NoBackCover = "No “Back cover” file, so the back stays blank.";
    private const string Filler = "Filler: a sheet holds 4 pages.";
    private const string SpacerHint = "Keeps the text facing its picture.";
    private const string Unreadable = "Could not be read as an image.";

    private const string MovedPrefix = "Moved to its own page so the words fit: ";

    /// <summary>"Moved to its own page so the words fit: pages 3, 7." for the printed numbers of the pages the fit moved; null when none.</summary>
    public static string? MovedToOwnPage(IReadOnlyList<int> printedNumbers)
    {
        if (printedNumbers.Count == 0)
        {
            return null;
        }

        var label = printedNumbers.Count == 1 ? "page " : "pages ";
        return MovedPrefix + label + string.Join(", ", printedNumbers.Select(n => n.ToString(System.Globalization.CultureInfo.CurrentCulture))) + ".";
    }

    public static (string? Note, bool IsWarned) For(BookPage page, bool hasImage, bool isMissingBackCoverBlank)
    {
        if (page.Kind == PageKind.Spacer)
        {
            return (SpacerHint, false);
        }

        if (page.Kind == PageKind.Text)
        {
            return (null, false);
        }

        if (page.Kind == PageKind.Blank)
        {
            return isMissingBackCoverBlank ? (NoBackCover, true) : (Filler, false);
        }

        if (!hasImage)
        {
            return (Unreadable, true);
        }

        return page.Kind switch
        {
            PageKind.Other => (Unrecognised, true),
            PageKind.DuplicateCover => (DuplicateCover, true),
            PageKind.DuplicateBackCover => (DuplicateBackCover, true),
            _ => (null, false),
        };
    }
}
