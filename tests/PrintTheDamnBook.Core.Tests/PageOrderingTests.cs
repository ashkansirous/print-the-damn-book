namespace PrintTheDamnBook.Core.Tests;

public class PageOrderingTests
{
    [Fact]
    public void SampleFolder_OrdersCoverNumbersNaturallyThenBackCover()
    {
        string[] files = ["10.jpg", "Back cover.jpg", "2.jpg", "1.jpg", "Cover.jpg", "9.jpg", "3.jpg"];

        var labels = PageOrdering.FromFolder(files).Select(p => p.Label);

        Assert.Equal(["Cover", "1", "2", "3", "9", "10", "Back cover"], labels);
    }

    [Fact]
    public void UnrecognisedNames_GoAfterNumberedBeforeBackCover_Alphabetically()
    {
        string[] files = ["Back cover.png", "zeta.jpg", "1.jpg", "alpha.jpg", "Cover.png"];

        var pages = PageOrdering.FromFolder(files);

        Assert.Equal(["Cover", "1", "alpha", "zeta", "Back cover"], pages.Select(p => p.Label));
        Assert.Equal(PageKind.Other, pages[2].Kind);
        Assert.Equal(PageKind.BackCover, pages[^1].Kind);
    }

    [Fact]
    public void Matching_IsCaseInsensitive()
    {
        var pages = PageOrdering.FromFolder(["BACK COVER.jpg", "cover.jpg"]);

        Assert.Equal([PageKind.Cover, PageKind.BackCover], pages.Select(p => p.Kind));
    }

    [Fact]
    public void CoverPrefixedNames_AreNotCovers()
    {
        var pages = PageOrdering.FromFolder(["Cover letter draft.png", "Coverage.png", "Cover.jpg", "1.jpg"]);

        Assert.Equal(["Cover", "1", "Cover letter draft", "Coverage"], pages.Select(p => p.Label));
        Assert.Equal(PageKind.Cover, pages[0].Kind);
        Assert.Equal(2, pages.Count(p => p.Kind == PageKind.Other));
    }

    [Fact]
    public void DuplicateCoverAndBackCover_ExtrasBecomeUnrecognised()
    {
        var pages = PageOrdering.FromFolder(["Back cover.png", "Back cover.jpg", "Cover.png", "Cover.jpg", "1.jpg"]);

        Assert.Equal(PageKind.Cover, pages[0].Kind);
        Assert.Equal("Cover.jpg", pages[0].Path);
        Assert.Equal(PageKind.BackCover, pages[^1].Kind);
        Assert.Equal("Back cover.jpg", pages[^1].Path);
        Assert.Equal(PageKind.DuplicateCover, pages.Single(p => p.Path == "Cover.png").Kind);
        Assert.Equal("Cover.png", pages.Single(p => p.Path == "Cover.png").Label);
        Assert.Equal(PageKind.DuplicateBackCover, pages.Single(p => p.Path == "Back cover.png").Kind);
        Assert.Equal("Back cover.png", pages.Single(p => p.Path == "Back cover.png").Label);
    }

    [Theory]
    [InlineData(" 7.jpg")]
    [InlineData("+3.jpg")]
    [InlineData("-1.jpg")]
    public void NonDigitStems_AreNotNumbered(string file)
    {
        var pages = PageOrdering.FromFolder([file]);

        Assert.Equal(PageKind.Other, pages.Single().Kind);
    }
}
