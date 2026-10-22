namespace PrintTheDamnBook.Core.Tests;

public class PageListTests
{
    private static PageList Sample(int numbered = 5) =>
        new(PageOrdering.FromFolder(
            ["Cover.jpg", "Back cover.jpg", .. Enumerable.Range(1, numbered).Select(n => $"{n}.jpg")]));

    private static string[] Labels(PageList list) => list.Pages.Select(p => p.Label).ToArray();

    [Fact]
    public void Move_ReordersMiddlePages()
    {
        var list = Sample();

        Assert.True(list.Move(3, 2));

        Assert.Equal(["Cover", "1", "3", "2", "4", "5", "Back cover"], Labels(list));
    }

    [Fact]
    public void Move_RefusesPinnedEndsAsSourceOrTarget()
    {
        var list = Sample();

        Assert.False(list.Move(0, 3));
        Assert.False(list.Move(6, 3));
        Assert.False(list.Move(3, 0));
        Assert.False(list.Move(3, 6));
        Assert.Equal(["Cover", "1", "2", "3", "4", "5", "Back cover"], Labels(list));
    }

    [Fact]
    public void Move_RefusesOutOfRangeAndNoOp()
    {
        var list = Sample();

        Assert.False(list.Move(-1, 2));
        Assert.False(list.Move(2, 99));
        Assert.False(list.Move(2, 2));
    }

    [Fact]
    public void Move_WithoutCovers_AllowsMovingTheEnds()
    {
        var list = new PageList(PageOrdering.FromFolder(["1.jpg", "2.jpg", "3.jpg"]));

        Assert.True(list.Move(0, 2));

        Assert.Equal(["2", "3", "1"], Labels(list));
    }

    [Fact]
    public void Remove_RepadsBeforeBackCover()
    {
        var list = Sample(10);

        Assert.True(list.Remove(10));

        Assert.Equal(11, list.Pages.Count);
        Assert.Equal(12, list.Padded.Count);
        Assert.Equal(PageKind.Blank, list.Padded[^2].Kind);
        Assert.Equal(PageKind.BackCover, list.Padded[^1].Kind);
    }

    [Fact]
    public void Remove_OutOfRange_ReturnsFalse()
    {
        Assert.False(Sample().Remove(42));
    }

    [Fact]
    public void Add_InsertsBeforeBackCover_AsUnrecognisedPages()
    {
        var list = Sample(2);

        list.Add([@"C:\x\extra.png"]);

        Assert.Equal(["Cover", "1", "2", "extra", "Back cover"], Labels(list));
        Assert.Equal(PageKind.Added, list.Pages[3].Kind);
    }

    [Fact]
    public void Add_WithoutBackCover_AppendsAtEnd()
    {
        var list = new PageList(PageOrdering.FromFolder(["1.jpg"]));

        list.Add(["a.jpg", "b.jpg"]);

        Assert.Equal(["1", "a", "b"], Labels(list));
    }

    [Fact]
    public void Add_RaisesNoBookCheckFinding()
    {
        var list = Sample(3);
        list.Add([@"C:\x\extra.png"]);

        var findings = BookChecks.Find(list.Pages, Imposition.Compute(list.Pages).Length);

        Assert.DoesNotContain(findings, f => f.Issue == BookIssue.UnrecognisedFiles);
    }

    [Fact]
    public void MoveByIdentity_IgnoresBlanksAndRefusesPinnedBackCover()
    {
        var list = Sample(8);
        list.Remove(1);
        var pages = list.Pages;

        Assert.True(list.IsPinned(pages[^1]));
        Assert.False(list.Move(pages[2], pages[^1]));
        Assert.False(list.Remove(BookPage.Blank));
        Assert.True(list.Move(pages[3], pages[1]));
        Assert.Equal("4", list.Pages[1].Label);
    }

    [Fact]
    public void Identity_DistinguishesTheSameFileAddedTwice()
    {
        var list = Sample(1);
        list.Add([@"C:\x\a.png", @"C:\x\a.png"]);
        var second = list.Pages[3];

        Assert.True(list.Remove(second));

        Assert.Equal(["Cover", "1", "a", "Back cover"], Labels(list));
        Assert.False(list.Remove(second));
    }

    [Fact]
    public void Remove_AllowsCovers_AndPagesIsASnapshot()
    {
        var list = Sample(2);
        var snapshot = list.Pages;

        Assert.True(list.Remove(0));

        Assert.Equal(4, snapshot.Count);
        Assert.Equal(3, list.Pages.Count);
    }

    [Fact]
    public void IsPinned_OnlyCoverAtFrontAndBackCoverAtEnd()
    {
        var list = Sample();

        Assert.True(list.IsPinned(0));
        Assert.True(list.IsPinned(6));
        Assert.False(list.IsPinned(3));
        Assert.False(list.IsPinned(99));
        Assert.False(new PageList(PageOrdering.FromFolder(["1.jpg", "2.jpg"])).IsPinned(0));
    }

    [Fact]
    public void Constructor_DropsBlanks()
    {
        var list = new PageList([BookPage.Blank, new BookPage("1.jpg", "1", PageKind.Numbered)]);

        Assert.Single(list.Pages);
    }

    [Fact]
    public void MoveToEnd_GoesBeforePinnedBackCover()
    {
        var list = Sample();

        Assert.True(list.MoveToEnd(list.Pages[1]));

        Assert.Equal(["Cover", "2", "3", "4", "5", "1", "Back cover"], Labels(list));
    }

    [Fact]
    public void MoveToEnd_GoesLastWithoutBackCover()
    {
        var list = new PageList(PageOrdering.FromFolder(["1.jpg", "2.jpg", "3.jpg"]));

        Assert.True(list.MoveToEnd(list.Pages[0]));

        Assert.Equal(["2", "3", "1"], Labels(list));
    }

    [Fact]
    public void MoveToEnd_RefusesPinnedAndAlreadyLast()
    {
        var list = Sample();

        Assert.False(list.MoveToEnd(list.Pages[0]));
        Assert.False(list.MoveToEnd(list.Pages[5]));
    }
}
