using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests;

public class LayoutFillTests
{
    private static readonly SizeD Half = new(148.5, 210);
    private static readonly SizeD Sheet = new(297, 210);

    [Fact]
    public void Fill_covers_the_available_area_and_crops_the_overflowing_axis_equally()
    {
        var rect = HalfSheetLayout.Fill(new SizeD(1000, 1000), Half, 5);

        var available = HalfSheetLayout.Available(Half, 5);
        Assert.Equal(available.Height, rect.Height, 9);
        Assert.True(rect.Width > available.Width);
        Assert.Equal(available.X - rect.X, rect.X + rect.Width - (available.X + available.Width), 9);
        Assert.Equal(Half.Width / 2, rect.X + rect.Width / 2, 9);
    }

    [Fact]
    public void Fill_keeps_the_aspect_ratio()
    {
        var rect = HalfSheetLayout.Fill(new SizeD(600, 900), Half, 5);

        Assert.Equal(600.0 / 900, rect.Width / rect.Height, 9);
    }

    [Fact]
    public void Fill_of_a_wide_image_overflows_sideways_and_a_tall_one_overflows_vertically()
    {
        var wide = HalfSheetLayout.Fill(new SizeD(2000, 1000), Half, 5);
        var tall = HalfSheetLayout.Fill(new SizeD(1000, 3000), Half, 5);

        Assert.True(wide.X < 5);
        Assert.Equal(200, wide.Height, 9);
        Assert.True(tall.Y < 5);
        Assert.Equal(Half.Width - 10, tall.Width, 9);
    }

    [Fact]
    public void Fill_that_exactly_matches_the_aspect_equals_fit()
    {
        var size = new SizeD(138.5, 200);

        var fill = HalfSheetLayout.Fill(size, Half, 5);
        var fit = HalfSheetLayout.Fit(size, Half, 5);

        Assert.Equal(fit.X, fill.X, 9);
        Assert.Equal(fit.Width, fill.Width, 9);
        Assert.Equal(fit.Height, fill.Height, 9);
    }

    [Fact]
    public void Fill_with_no_margin_covers_the_whole_half()
    {
        var rect = HalfSheetLayout.Fill(new SizeD(1000, 1000), Half, 0);

        Assert.True(rect.Width >= Half.Width - 1e-9);
        Assert.True(rect.Height >= Half.Height - 1e-9);
    }

    [Theory]
    [InlineData(0, 100, 5)]
    [InlineData(100, 0, 5)]
    [InlineData(100, 100, -1)]
    [InlineData(100, 100, 80)]
    public void Fill_rejects_unusable_input(double width, double height, double margin)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HalfSheetLayout.Fill(new SizeD(width, height), new SizeD(100, 100), margin));
    }

    [Fact]
    public void Resolve_in_fit_mode_shows_the_whole_image()
    {
        var placement = HalfSheetLayout.Resolve(new SizeD(100, 150), Sheet, true, 5, ScalingMode.Fit);

        Assert.Equal(HalfSheetLayout.Place(new SizeD(100, 150), Sheet, true, 5), placement.Image);
        Assert.Equal(placement.Image, placement.Visible);
    }

    [Fact]
    public void Resolve_in_fill_mode_clips_to_the_margin_box_of_the_right_half()
    {
        var placement = HalfSheetLayout.Resolve(new SizeD(1000, 1000), Sheet, true, 5, ScalingMode.Fill);

        Assert.Equal(new RectD(148.5 + 5, 5, 138.5, 200), placement.Visible);
        Assert.True(placement.Image.X < placement.Visible.X);
        Assert.Equal(placement.Visible.X - placement.Image.X, placement.LocalClip.X, 9);
        Assert.Equal(0, placement.LocalClip.Y, 9);
    }

    [Fact]
    public void Rotating_a_fill_placement_turns_image_and_visible_area_about_the_sheet_centre()
    {
        var placement = HalfSheetLayout.Resolve(new SizeD(1000, 1000), Sheet, false, 5, ScalingMode.Fill);

        var turned = placement.Rotate180(Sheet);

        Assert.Equal(Sheet.Width - 5 - 138.5, turned.Visible.X, 9);
        Assert.Equal(placement.LocalClip.X, turned.LocalClip.X, 9);
        Assert.Equal(placement.LocalClip.Y, turned.LocalClip.Y, 9);
        Assert.Equal(placement.LocalClip.Width, turned.LocalClip.Width, 9);
    }

    [Theory]
    [InlineData(double.NaN, HalfSheetLayout.DefaultMarginMm)]
    [InlineData(-3, 0)]
    [InlineData(7.5, 7.5)]
    [InlineData(500, HalfSheetLayout.MaxMarginMm)]
    public void NormalizeMargin_clamps_to_a_usable_range(double input, double expected)
    {
        Assert.Equal(expected, HalfSheetLayout.NormalizeMargin(input));
    }
}
