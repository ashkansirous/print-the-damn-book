namespace PrintTheDamnBook.Core.Tests;

public class SpreadsTests
{
    private static BookPage Page(string label, PageKind kind = PageKind.Numbered) => new($"{label}.jpg", label, kind);

    [Fact]
    public void Empty_book_has_no_spreads()
    {
        Assert.Empty(Spreads.Of([]));
    }

    [Fact]
    public void Twelve_pages_make_front_cover_five_spreads_and_back_cover()
    {
        var pages = new List<BookPage> { Page("Cover", PageKind.Cover) };
        pages.AddRange(Enumerable.Range(1, 10).Select(n => Page(n.ToString())));
        pages.Add(Page("Back cover", PageKind.BackCover));

        var spreads = Spreads.Of(pages);

        Assert.Equal(7, spreads.Count);
        Assert.Equal(SpreadKind.FrontCover, spreads[0].Kind);
        Assert.Null(spreads[0].Left);
        Assert.Same(pages[0], spreads[0].Right);
        Assert.Equal(SpreadKind.BackCover, spreads[^1].Kind);
        Assert.Same(pages[^1], spreads[^1].Left);
        Assert.Null(spreads[^1].Right);
    }

    [Fact]
    public void Inside_spreads_are_numbered_and_pair_facing_pages()
    {
        var pages = Enumerable.Range(0, 8).Select(n => Page(n.ToString())).ToList();

        var inside = Spreads.Of(pages).Where(s => s.Kind == SpreadKind.Inside).ToList();

        Assert.Equal([1, 2, 3], inside.Select(s => s.Number));
        Assert.Same(pages[1], inside[0].Left);
        Assert.Same(pages[2], inside[0].Right);
        Assert.Same(pages[5], inside[2].Left);
        Assert.Same(pages[6], inside[2].Right);
    }
}
