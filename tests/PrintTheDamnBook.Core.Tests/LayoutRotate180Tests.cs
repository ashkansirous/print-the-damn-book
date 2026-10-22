using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests;

public class LayoutRotate180Tests
{
    private static readonly SizeD Sheet = new(297, 210);

    [Fact]
    public void Left_half_page_lands_in_the_right_half_mirrored_through_the_centre()
    {
        var left = HalfSheetLayout.Place(new SizeD(100, 150), Sheet, false, 5);

        var turned = HalfSheetLayout.Rotate180(left, Sheet);

        Assert.Equal(Sheet.Width - left.X - left.Width, turned.X, 9);
        Assert.Equal(Sheet.Height - left.Y - left.Height, turned.Y, 9);
        Assert.Equal(left.Width, turned.Width);
        Assert.Equal(left.Height, turned.Height);
        Assert.True(turned.X >= Sheet.Width / 2);
    }

    [Fact]
    public void Turning_a_left_page_gives_the_right_pages_slot_when_both_have_the_same_size()
    {
        var size = new SizeD(100, 150);
        var left = HalfSheetLayout.Place(size, Sheet, false, 5);
        var right = HalfSheetLayout.Place(size, Sheet, true, 5);

        var turned = HalfSheetLayout.Rotate180(left, Sheet);

        Assert.Equal(right.X, turned.X, 9);
        Assert.Equal(right.Y, turned.Y, 9);
    }

    [Fact]
    public void Turning_twice_returns_the_original()
    {
        var rect = new RectD(12, 30, 80, 120);

        var twice = HalfSheetLayout.Rotate180(HalfSheetLayout.Rotate180(rect, Sheet), Sheet);

        Assert.Equal(rect, twice);
    }
}
