using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests.Kdp;

public class KdpCoverSizeTests
{
    private const ReadingDirection Ltr = ReadingDirection.LeftToRight;
    private const ReadingDirection Rtl = ReadingDirection.RightToLeft;

    /// <summary>
    /// Checks the code against KDP's published formula (G201953020: 0.125 + 8.5 + pages x 0.002347 + 8.5 + 0.125), worked by hand. KDP's cover
    /// calculator itself gave no 8.5 x 8.5 value on 2026-11-19, so these are formula checks, not calculator values (Slice 29, Verification).
    /// </summary>
    [Theory]
    [InlineData(24, 0.056328, 17.306328)]
    [InlineData(32, 0.075104, 17.325104)]
    [InlineData(40, 0.09388, 17.34388)]
    public void The_spine_and_cover_width_match_the_published_formula(int pages, double spine, double width)
    {
        Assert.Equal(spine, KdpCoverSize.SpineWidth(pages), 6);
        Assert.Equal(width, KdpCoverSize.CoverWidth(pages), 6);
        Assert.Equal(8.75, KdpCoverSize.CoverHeight, 6);
    }

    [Theory]
    [InlineData(24, Ltr)]
    [InlineData(40, Ltr)]
    [InlineData(24, Rtl)]
    [InlineData(40, Rtl)]
    public void The_panels_tile_the_whole_cover_and_each_picture_panel_is_the_interior_page_size(int pages, ReadingDirection direction)
    {
        var back = KdpCoverSize.BackPanel(pages, direction);
        var front = KdpCoverSize.FrontPanel(pages, direction);
        var spine = KdpCoverSize.Spine(pages);
        var (first, second) = back.X < front.X ? (back, front) : (front, back);

        Assert.Equal(0, first.X, 9);
        Assert.Equal(first.Width, spine.X, 9);
        Assert.Equal(spine.X + spine.Width, second.X, 9);
        Assert.Equal(KdpCoverSize.CoverWidth(pages), second.X + second.Width, 9);
        Assert.Equal((KdpTrim.PictureWidthPx, KdpTrim.PictureHeightPx), KdpCoverSize.PanelPixels(back));
        Assert.Equal((KdpTrim.PictureWidthPx, KdpTrim.PictureHeightPx), KdpCoverSize.PanelPixels(front));
    }

    [Fact]
    public void A_left_to_right_book_has_its_back_on_the_left_and_its_front_on_the_right()
    {
        Assert.True(KdpCoverSize.BackPanel(24, Ltr).X < KdpCoverSize.Spine(24).X);
        Assert.True(KdpCoverSize.FrontPanel(24, Ltr).X > KdpCoverSize.Spine(24).X);
    }

    [Fact]
    public void A_right_to_left_book_has_its_front_on_the_left_and_its_back_on_the_right()
    {
        Assert.True(KdpCoverSize.FrontPanel(24, Rtl).X < KdpCoverSize.Spine(24).X);
        Assert.True(KdpCoverSize.BackPanel(24, Rtl).X > KdpCoverSize.Spine(24).X);
    }

    [Theory]
    [InlineData(Ltr)]
    [InlineData(Rtl)]
    public void The_barcode_box_is_2_by_1_2_inches_inside_the_back_trim_and_clear_of_the_spine_and_trim(ReadingDirection direction)
    {
        var box = KdpCoverSize.Barcode(24, direction);
        var trim = KdpCoverSize.BackTrim(24, direction);

        Assert.Equal(2, box.Width);
        Assert.Equal(1.2, box.Height);
        Assert.True(box.X - trim.X >= 0.25);
        Assert.True(trim.X + trim.Width - (box.X + box.Width) >= 0.25);
        Assert.True(trim.Y + trim.Height - (box.Y + box.Height) >= 0.25);
    }

    [Fact]
    public void The_barcode_is_lower_right_of_the_back_in_a_left_to_right_book()
    {
        var box = KdpCoverSize.Barcode(24, Ltr);
        var trim = KdpCoverSize.BackTrim(24, Ltr);

        Assert.True(box.X + box.Width / 2 > trim.X + trim.Width / 2);
    }

    [Fact]
    public void The_barcode_is_lower_left_of_the_back_in_a_right_to_left_book()
    {
        var box = KdpCoverSize.Barcode(24, Rtl);
        var trim = KdpCoverSize.BackTrim(24, Rtl);

        Assert.True(box.X + box.Width / 2 < trim.X + trim.Width / 2);
    }

    [Fact]
    public void A_book_of_24_to_40_pages_has_no_spine_text()
    {
        Assert.False(KdpCoverSize.HasSpineText(40));
        Assert.True(KdpCoverSize.HasSpineText(KdpCoverSize.SpineTextMinPages));
    }
}
