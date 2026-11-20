using PrintTheDamnBook.Core.Kdp;

namespace PrintTheDamnBook.Core.Tests.Kdp;

/// <summary>Slice 36 B and C: the picture keys, the outline of the kept window on a small uncut picture and the grid of the crop sheet.</summary>
public class KdpCropSheetTests
{
    [Fact]
    public void A_dino_picture_at_the_top_crop_outlines_the_upper_three_quarters()
    {
        var outline = KdpCropOutline.For(1792, 2400, 0, KdpCropSheetLayout.CellWidthPx, KdpCropSheetLayout.CellHeightPx);

        Assert.Equal(new PixelRect(0, 0, 199, 202), outline.Window);
        Assert.Equal((199, 267), (outline.ImageWidth, outline.ImageHeight));
        Assert.Equal((0, 582), (outline.AppliedOffsetPx, outline.MaxOffsetPx));
    }

    [Fact]
    public void An_offset_moves_the_outline_down_by_the_scaled_offset_and_keeps_its_size()
    {
        var top = KdpCropOutline.For(1792, 2400, 0, 480, 720);
        var moved = KdpCropOutline.For(1792, 2400, 300, 480, 720);

        Assert.Equal(top.Window.Height, moved.Window.Height);
        Assert.Equal(80, moved.Window.Y);
        Assert.Equal(300, moved.AppliedOffsetPx);
    }

    [Fact]
    public void The_largest_offset_ends_the_window_at_the_picture_bottom()
    {
        var outline = KdpCropOutline.For(1792, 2400, 100000, 480, 720);

        Assert.Equal(582, outline.AppliedOffsetPx);
        Assert.Equal(outline.ImageHeight, outline.Window.Y + outline.Window.Height);
    }

    [Fact]
    public void A_picture_wider_than_the_page_is_cropped_at_the_sides_and_has_no_spare_height()
    {
        var outline = KdpCropOutline.For(2400, 1792, 50, 480, 720);

        Assert.Equal(0, outline.MaxOffsetPx);
        Assert.Equal(0, outline.Window.Y);
        Assert.Equal(outline.ImageHeight, outline.Window.Height);
        Assert.True(outline.Window.X > 0);
    }

    [Fact]
    public void A_small_picture_is_never_enlarged_by_the_outline()
    {
        var outline = KdpCropOutline.For(100, 130, 0, 480, 720);

        Assert.Equal((100, 130), (outline.ImageWidth, outline.ImageHeight));
    }

    [Fact]
    public void The_sheet_puts_six_pictures_in_a_row_and_the_cells_do_not_overlap()
    {
        var layout = KdpCropSheetLayout.For(14);

        Assert.Equal((6, 3), (layout.Columns, layout.Rows));
        Assert.Equal(6 * 200 + 7 * 8, layout.SheetWidth);
        var boxes = Enumerable.Range(0, 14).Select(layout.CellBox).ToList();
        Assert.Equal(14, boxes.Distinct().Count());
        Assert.All(boxes, b => Assert.True(b.X + b.Width <= layout.SheetWidth && b.Y + b.Height <= layout.SheetHeight));
        Assert.True(layout.LabelBox(7).Y + layout.LabelBox(7).Height == layout.CellBox(7).Y);
    }

    [Fact]
    public void A_book_with_fewer_pictures_than_a_row_is_one_short_row()
    {
        var layout = KdpCropSheetLayout.For(3);

        Assert.Equal((3, 1), (layout.Columns, layout.Rows));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.CellBox(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => KdpCropSheetLayout.For(0));
    }

    [Fact]
    public void The_label_names_the_key_and_the_offset_in_use_out_of_the_most_allowed()
    {
        Assert.Equal("7 y=60/582", KdpCropSheetLayout.Label("7", 60, 582));
        Assert.Equal("crop/front.png", KdpCropSheetLayout.DetailPath(KdpPictureKeys.Front));
    }

    [Theory]
    [InlineData("7", 7, null, true)]
    [InlineData("front", 0, "front", true)]
    [InlineData("back", 0, "back", true)]
    [InlineData("0", 0, null, false)]
    [InlineData("-3", 0, null, false)]
    [InlineData("cover", 0, null, false)]
    [InlineData("", 0, null, false)]
    public void An_offset_key_is_a_story_page_or_a_cover_picture(string text, int page, string? cover, bool ok)
    {
        var parsed = KdpPictureKeys.TryParse(text, out var storyPage, out var coverKey);

        Assert.Equal(ok, parsed);
        if (ok)
        {
            Assert.Equal((page, cover), (storyPage, coverKey));
        }
    }
}
