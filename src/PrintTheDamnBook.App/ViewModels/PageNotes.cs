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
    private const string Unreadable = "Could not be read as an image.";

    public static (string? Note, bool IsWarned) For(BookPage page, bool hasImage, bool isMissingBackCoverBlank)
    {
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
