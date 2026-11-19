using PrintTheDamnBook.Kdp;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp.Tests;

public class KdpImageDecoderTests
{
    [Fact]
    public void A_picture_comes_out_at_exactly_the_target_size_with_the_crop_recorded()
    {
        var picture = KdpImageDecoder.Prepare(SyntheticBook.Picture(0));

        using var decoded = SKBitmap.Decode(picture.Jpeg);
        Assert.Equal(2588, decoded.Width);
        Assert.Equal(2625, decoded.Height);
        Assert.Equal(1792, picture.SourceWidth);
        Assert.Equal(2400, picture.SourceHeight);
        Assert.Equal(1818, picture.Crop.Source.Height);
    }

    [Fact]
    public void The_top_of_the_picture_is_kept_and_the_bottom_dropped()
    {
        using var source = new SKBitmap(1792, 2400);
        using (var canvas = new SKCanvas(source))
        {
            canvas.Clear(SKColors.Red);
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(0, 1900, 1792, 500, blue);
        }

        using var image = SKImage.FromBitmap(source);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var picture = KdpImageDecoder.Prepare(data.ToArray());

        using var decoded = SKBitmap.Decode(picture.Jpeg);
        var bottom = decoded.GetPixel(1000, 2600);
        Assert.True(bottom.Red > 200 && bottom.Blue < 80, "the blue band at the foot of the picture must be cropped away");
    }

    [Fact]
    public void An_exif_rotated_picture_is_stood_upright()
    {
        // A 2400 x 1792 JPEG marked RightTop (rotate 90 clockwise) is a 1792 x 2400 picture once upright.
        var rotated = ExifRotated(SyntheticBook.Picture(0, 2400, 1792));

        var picture = KdpImageDecoder.Prepare(rotated);

        Assert.Equal(1792, picture.SourceWidth);
        Assert.Equal(2400, picture.SourceHeight);
    }

    [Fact]
    public void Something_that_is_not_an_image_is_refused()
    {
        Assert.Throws<KdpException>(() => KdpImageDecoder.Prepare([1, 2, 3, 4]));
    }

    /// <summary>Inserts an EXIF block with orientation 6 right after the JPEG's start marker.</summary>
    private static byte[] ExifRotated(byte[] jpeg)
    {
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22,
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
            0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x06, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        return [.. jpeg.Take(2), .. exif, .. jpeg.Skip(2)];
    }
}
