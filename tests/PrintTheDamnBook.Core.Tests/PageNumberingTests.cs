using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests;

public class PageNumberingTests
{
    private static BookPage Page(string name, PageKind kind = PageKind.Numbered)
    {
        return new BookPage(name + ".png", name, kind);
    }

    private static List<BookPage> Book(int interior)
    {
        var pages = new List<BookPage> { Page("cover", PageKind.Cover) };
        pages.AddRange(Enumerable.Range(1, interior).Select(i => Page("p" + i)));
        pages.Add(Page("back", PageKind.BackCover));
        return pages;
    }

    [Fact]
    public void Interior_pages_are_numbered_from_one_and_covers_and_blanks_get_none()
    {
        var pages = Book(6);
        var padded = Imposition.Pad(pages);

        var numbers = PageNumbering.Compute(padded);

        Assert.False(numbers.ContainsKey(pages[0]));
        Assert.False(numbers.ContainsKey(pages[^1]));
        Assert.Equal([1, 2, 3, 4, 5, 6], pages.Skip(1).Take(6).Select(p => numbers[p]));
    }

    [Fact]
    public void Blank_padding_gets_no_number()
    {
        var pages = Book(5);
        var padded = Imposition.Pad(pages);

        var numbers = PageNumbering.Compute(padded);

        Assert.Equal(5, numbers.Count);
        Assert.DoesNotContain(padded, p => p.Kind == PageKind.Blank && numbers.ContainsKey(p));
    }

    [Fact]
    public void Without_a_cover_the_first_page_is_one()
    {
        var pages = new List<BookPage> { Page("a"), Page("b"), Page("c"), Page("d") };

        var numbers = PageNumbering.Compute(pages);

        Assert.Equal([1, 2, 3, 4], pages.Select(p => numbers[p]));
    }

    [Fact]
    public void Numbers_follow_position_so_a_reorder_renumbers()
    {
        var pages = Book(4);
        var swapped = new List<BookPage> { pages[0], pages[2], pages[1], pages[3], pages[4], pages[5] };

        var numbers = PageNumbering.Compute(Imposition.Pad(swapped));

        Assert.Equal(1, numbers[pages[2]]);
        Assert.Equal(2, numbers[pages[1]]);
    }

    [Fact]
    public void Padding_blanks_before_the_back_cover_leave_the_first_interior_page_as_one()
    {
        var pages = Book(5);
        var padded = Imposition.Pad(pages);

        var numbers = PageNumbering.Compute(padded);

        Assert.Equal(PageKind.Blank, padded[^2].Kind);
        Assert.Equal(1, numbers[pages[1]]);
        Assert.Equal([1, 2, 3, 4, 5], pages.Skip(1).Take(5).Select(p => numbers[p]));
    }

    [Fact]
    public void Size_scales_with_the_sheet_height()
    {
        var a4 = Paper.LandscapeSheetMm(PaperKind.A4);
        var letter = Paper.LandscapeSheetMm(PaperKind.Letter);

        Assert.Equal(PageNumbering.ReferenceBandMm, PageNumbering.BandMm(a4), 6);
        Assert.Equal(PageNumbering.ReferenceFontMm, PageNumbering.FontMm(a4), 6);
        Assert.True(PageNumbering.BandMm(letter) > PageNumbering.BandMm(a4));
    }
}
