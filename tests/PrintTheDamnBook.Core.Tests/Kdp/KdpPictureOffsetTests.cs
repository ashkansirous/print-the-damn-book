using PrintTheDamnBook.Core.Kdp;

namespace PrintTheDamnBook.Core.Tests.Kdp;

public class KdpPictureOffsetTests
{
    [Fact]
    public void An_offset_moves_the_kept_part_down_and_keeps_its_size()
    {
        var crop = KdpPictureCrop.For(1792, 2400, 60);

        Assert.Equal(new PixelRect(0, 60, 1792, 1818), crop.Source);
        Assert.Equal(2588, crop.TargetWidth);
    }

    [Fact]
    public void An_offset_past_the_spare_height_stops_where_the_square_meets_the_bottom()
    {
        var crop = KdpPictureCrop.For(1792, 2400, 5000);

        Assert.Equal(2400 - 1818, crop.Source.Y);
        Assert.Equal(2400, crop.Source.Y + crop.Source.Height);
    }

    [Fact]
    public void A_negative_offset_is_zero()
    {
        Assert.Equal(0, KdpPictureCrop.For(1792, 2400, -30).Source.Y);
    }

    [Fact]
    public void The_largest_offset_is_the_spare_height()
    {
        Assert.Equal(582, KdpPictureCrop.MaxOffset(1792, 2400));
    }

    [Fact]
    public void A_picture_with_no_spare_height_has_no_offset()
    {
        Assert.Equal(0, KdpPictureCrop.MaxOffset(3000, 2000));
        Assert.Equal(0, KdpPictureCrop.For(3000, 2000, 100).Source.Y);
    }

    [Fact]
    public void The_scale_is_the_target_width_over_the_kept_width()
    {
        Assert.Equal(2588.0 / 1792, KdpPictureCrop.For(1792, 2400).Scale, 6);
    }
}
