namespace PrintTheDamnBook.Core.Tests;

public class BookChecksTests
{
    private static readonly BookPage Cover = new("c.jpg", "Cover", PageKind.Cover);
    private static readonly BookPage Back = new("b.jpg", "Back cover", PageKind.BackCover);
    private static readonly BookPage Numbered = new("1.jpg", "1", PageKind.Numbered);
    private static readonly BookPage Stray = new("x.jpg", "x", PageKind.Other);

    [Fact]
    public void CompleteBook_HasNoIssues()
    {
        Assert.Empty(BookChecks.Find([Cover, Numbered, Back], 1));
    }

    [Fact]
    public void AllApplicableIssues_AreReported()
    {
        var issues = BookChecks.Find([Numbered, Stray], 7).Select(f => f.Issue);

        Assert.Equal(
            [BookIssue.TooThick, BookIssue.MissingCover, BookIssue.MissingBackCover, BookIssue.UnrecognisedFiles],
            issues);
    }

    [Fact]
    public void DemotedDuplicates_GetTheirOwnIssues_NotUnrecognised()
    {
        var dupCover = new BookPage("c2.png", "c2.png", PageKind.DuplicateCover);
        var dupBack = new BookPage("b2.png", "b2.png", PageKind.DuplicateBackCover);

        var issues = BookChecks.Find([Cover, dupCover, dupBack, Back], 1).Select(f => f.Issue);

        Assert.Equal([BookIssue.DuplicateCover, BookIssue.DuplicateBackCover], issues);
    }

    [Fact]
    public void UnrecognisedFiles_CarryTheirFileNames()
    {
        var other = new BookPage("book/notes.png", "notes", PageKind.Other);

        var finding = BookChecks.Find([Cover, Stray, other, Back], 1).Single();

        Assert.Equal(BookIssue.UnrecognisedFiles, finding.Issue);
        Assert.Equal(["x.jpg", "notes.png"], finding.Files);
    }

    [Fact]
    public void EmptyFolder_HasNoIssues()
    {
        Assert.Empty(BookChecks.Find([], 0));
    }
}
