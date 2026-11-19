using PrintTheDamnBook.Core.Kdp;

namespace PrintTheDamnBook.Core.Tests.Kdp;

public class KdpTrimAndCropTests
{
    [Fact]
    public void The_page_with_bleed_is_8_625_by_8_75_inches_and_its_picture_is_2588_by_2625_pixels()
    {
        Assert.Equal(8.625, KdpTrim.PageWidthInches);
        Assert.Equal(8.75, KdpTrim.PageHeightInches);
        Assert.Equal(KdpTrim.PictureWidthPx, (int)Math.Round(KdpTrim.PageWidthInches * KdpTrim.PixelsPerInch, MidpointRounding.AwayFromZero));
        Assert.Equal(KdpTrim.PictureHeightPx, (int)Math.Round(KdpTrim.PageHeightInches * KdpTrim.PixelsPerInch, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void The_bleed_is_on_the_outside_edge_of_the_page()
    {
        Assert.Equal(0, KdpTrim.TrimRect(rightHandPage: true).X);
        Assert.Equal(KdpTrim.BleedWidthInches, KdpTrim.TrimRect(rightHandPage: false).X);
        Assert.Equal(0.125, KdpTrim.TrimRect(true).Y);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Words_and_numbers_stay_inside_the_safe_margin_and_do_not_touch_each_other(bool right)
    {
        var trim = KdpTrim.TrimRect(right);
        var text = KdpTrim.TextBox(right);
        var number = KdpTrim.NumberBox(right);

        Assert.True(text.X >= trim.X + KdpTrim.SafeMarginInches);
        Assert.True(text.Y >= trim.Y + KdpTrim.SafeMarginInches);
        Assert.True(text.X + text.Width <= trim.X + trim.Width - KdpTrim.SafeMarginInches);
        Assert.True(text.Y + text.Height <= number.Y);
        Assert.True(number.Y + number.Height <= trim.Y + trim.Height - KdpTrim.SafeMarginInches + 1e-9);
        Assert.Equal(trim.X + trim.Width / 2, number.X + number.Width / 2, 6);
    }

    [Fact]
    public void A_3_by_4_picture_keeps_the_full_width_and_the_top()
    {
        var crop = KdpPictureCrop.For(1792, 2400);

        Assert.Equal(new PixelRect(0, 0, 1792, 1818), crop.Source);
        Assert.Equal(2588, crop.TargetWidth);
        Assert.Equal(2625, crop.TargetHeight);
        Assert.True(crop.Upscales);
        Assert.Equal(1792 / 8.625, crop.EffectiveDpi, 6);
    }

    [Fact]
    public void A_wide_picture_is_cut_at_the_sides_around_its_centre()
    {
        var crop = KdpPictureCrop.For(3000, 2000);

        Assert.Equal(0, crop.Source.Y);
        Assert.Equal(2000, crop.Source.Height);
        Assert.Equal(1972, crop.Source.Width);
        Assert.Equal((3000 - 1972) / 2, crop.Source.X);
    }

    [Fact]
    public void A_big_picture_does_not_upscale()
    {
        Assert.False(KdpPictureCrop.For(2588, 3465).Upscales);
    }

    [Fact]
    public void A_picture_without_a_size_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KdpPictureCrop.For(0, 10));
    }
}
