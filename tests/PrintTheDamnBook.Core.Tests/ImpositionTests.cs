namespace PrintTheDamnBook.Core.Tests;

public class ImpositionTests
{
    private static List<BookPage> Book(int count)
    {
        var pages = new List<BookPage> { new("c.jpg", "Cover", PageKind.Cover) };
        pages.AddRange(Enumerable.Range(1, count - 2).Select(i => new BookPage($"{i}.jpg", i.ToString(), PageKind.Numbered)));
        pages.Add(new BookPage("b.jpg", "Back cover", PageKind.BackCover));
        return pages;
    }

    private static string Describe(Sheet[] sheets) =>
        string.Join(" / ", sheets.SelectMany(s => new[] { s.Front, s.Back }).Select(f => $"{f.Left.Label}+{f.Right.Label}"));

    [Fact]
    public void FourPages_OneSheet()
    {
        Assert.Equal("Back cover+Cover / 1+2", Describe(Imposition.Compute(Book(4))));
    }

    [Fact]
    public void EightPages_TwoSheets()
    {
        Assert.Equal("Back cover+Cover / 1+6 / 5+2 / 3+4", Describe(Imposition.Compute(Book(8))));
    }

    [Fact]
    public void TwelvePages_MatchesHandPrintedSample()
    {
        Assert.Equal(
            "Back cover+Cover / 1+10 / 9+2 / 3+8 / 7+4 / 5+6",
            Describe(Imposition.Compute(Book(12))));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(6, 2)]
    [InlineData(7, 1)]
    [InlineData(10, 2)]
    public void Padding_PutsBlanksBeforeBackCover(int count, int expectedBlanks)
    {
        var padded = Imposition.Pad(Book(count));

        Assert.Equal(0, padded.Count % 4);
        Assert.Equal(PageKind.BackCover, padded[^1].Kind);
        Assert.Equal(expectedBlanks, padded.Count(p => p.Kind == PageKind.Blank));
        Assert.All(padded.Skip(count - 1).Take(expectedBlanks), p => Assert.Equal(PageKind.Blank, p.Kind));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void EveryPageUsedExactlyOnce(int count)
    {
        var book = Book(count);

        var used = Imposition.Compute(book)
            .SelectMany(s => new[] { s.Front.Left, s.Front.Right, s.Back.Left, s.Back.Right })
            .Where(p => p.Kind != PageKind.Blank)
            .ToList();

        Assert.Equal(book.Count, used.Count);
        Assert.Equal(book.Count, used.Distinct().Count());
    }

    [Fact]
    public void Empty_HasNoSheets()
    {
        Assert.Empty(Imposition.Compute([]));
    }

    [Fact]
    public void NoBackCover_BlanksGoAtTheEnd()
    {
        var pages = Enumerable.Range(1, 10).Select(i => new BookPage($"{i}.jpg", i.ToString(), PageKind.Numbered)).ToList();

        var padded = Imposition.Pad(pages);

        Assert.Equal(12, padded.Count);
        Assert.Equal("10", padded[9].Label);
        Assert.All(padded.Skip(10), p => Assert.Equal(PageKind.Blank, p.Kind));
    }

    [Fact]
    public void SinglePage_PadsAfterIt()
    {
        var sheets = Imposition.Compute([new BookPage("c.jpg", "Cover", PageKind.Cover)]);

        Assert.Single(sheets);
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    public void TooThick_AboveSixSheets(int sheets, bool expected)
    {
        Assert.Equal(expected, Imposition.IsTooThick(sheets));
    }
}
