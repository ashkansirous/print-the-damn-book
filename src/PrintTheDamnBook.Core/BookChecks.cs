namespace PrintTheDamnBook.Core;

public enum BookIssue
{
    TooThick,
    MissingCover,
    MissingBackCover,
    UnrecognisedFiles,
    DuplicateCover,
    DuplicateBackCover,
}

/// <summary>One issue found in a book, with the file names it concerns (empty when none apply).</summary>
public sealed record BookFinding(BookIssue Issue, IReadOnlyList<string> Files);

/// <summary>Things the user should know about before printing.</summary>
public static class BookChecks
{
    private static readonly IReadOnlyList<string> NoFiles = [];

    public static IReadOnlyList<BookFinding> Find(IReadOnlyList<BookPage> pages, int sheetCount)
    {
        var issues = new List<BookFinding>();

        if (Imposition.IsTooThick(sheetCount))
        {
            issues.Add(new BookFinding(BookIssue.TooThick, NoFiles));
        }

        if (pages.Count > 0 && pages[0].Kind != PageKind.Cover)
        {
            issues.Add(new BookFinding(BookIssue.MissingCover, NoFiles));
        }

        if (pages.Count > 0 && pages[^1].Kind != PageKind.BackCover)
        {
            issues.Add(new BookFinding(BookIssue.MissingBackCover, NoFiles));
        }

        if (pages.Any(p => p.Kind == PageKind.Other))
        {
            issues.Add(new BookFinding(BookIssue.UnrecognisedFiles, FileNames(pages, PageKind.Other)));
        }

        if (pages.Any(p => p.Kind == PageKind.DuplicateCover))
        {
            issues.Add(new BookFinding(BookIssue.DuplicateCover, NoFiles));
        }

        if (pages.Any(p => p.Kind == PageKind.DuplicateBackCover))
        {
            issues.Add(new BookFinding(BookIssue.DuplicateBackCover, NoFiles));
        }

        return issues;
    }

    private static IReadOnlyList<string> FileNames(IReadOnlyList<BookPage> pages, PageKind kind) =>
        pages.Where(p => p.Kind == kind).Select(p => System.IO.Path.GetFileName(p.Path ?? p.Label)).ToList();
}
