using PrintTheDamnBook.Core.Kdp;

namespace PrintTheDamnBook.Core.Tests.Kdp;

public class KdpContactSheetTests
{
    [Theory]
    [InlineData(24)]
    [InlineData(36)]
    [InlineData(40)]
    public void The_sheet_is_no_wider_than_the_limit_and_holds_every_page(int pages)
    {
        var layout = KdpContactSheetLayout.For(pages);

        Assert.True(layout.SheetWidth <= KdpContactSheetLayout.MaxSheetWidthPx);
        Assert.Equal(0, layout.Columns % 2);
        Assert.True(layout.Columns * layout.Rows >= pages + 1);
    }

    [Fact]
    public void A_cell_keeps_the_page_shape()
    {
        var layout = KdpContactSheetLayout.For(24);

        var shape = (double)layout.CellHeight / layout.CellWidth;
        Assert.Equal(KdpTrim.PageHeightInches / KdpTrim.PageWidthInches, shape, 2);
    }

    [Fact]
    public void Page_one_is_on_the_right_of_the_first_spread_and_pairs_follow_left_then_right()
    {
        var layout = KdpContactSheetLayout.For(24);

        var first = layout.PageBox(1);
        var second = layout.PageBox(2);
        var third = layout.PageBox(3);
        Assert.True(first.X > layout.CellWidth);
        Assert.Equal(first.Y, second.Y);
        Assert.True(second.X > first.X + layout.CellWidth - 1);
        Assert.Equal(second.X + layout.CellWidth + layout.Gap, third.X);
    }

    [Fact]
    public void No_two_pages_overlap_and_all_stay_on_the_sheet()
    {
        var layout = KdpContactSheetLayout.For(40);
        var boxes = Enumerable.Range(1, 40).Select(layout.PageBox).ToList();

        Assert.Equal(40, boxes.Distinct().Count());
        Assert.All(boxes, b =>
        {
            Assert.True(b.X >= 0 && b.X + b.Width <= layout.SheetWidth);
            Assert.True(b.Y - layout.LabelHeight >= 0 && b.Y + b.Height <= layout.SheetHeight);
        });
    }

    [Fact]
    public void The_label_sits_directly_above_its_page()
    {
        var layout = KdpContactSheetLayout.For(24);

        var label = layout.LabelBox(5);
        var box = layout.PageBox(5);
        Assert.Equal(box.Y, label.Y + label.Height);
        Assert.Equal(box.X, label.X);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void A_page_outside_the_book_is_refused(int position)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KdpContactSheetLayout.For(24).PageBox(position));
    }

    [Fact]
    public void A_book_with_no_pages_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KdpContactSheetLayout.For(0));
    }
}
