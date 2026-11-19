using PrintTheDamnBook.Core.Kdp;

namespace PrintTheDamnBook.Core.Tests.Kdp;

public class CutLineCheckTests
{
    private const int Width = 1792;
    private const int Height = 2400;
    private static readonly PixelRect Crop = KdpPictureCrop.For(Width, Height).Source;

    [Fact]
    public void A_plain_background_is_not_flagged()
    {
        var score = CutLineCheck.Evaluate(Image(_ => { }), Crop);

        Assert.False(score.Flagged);
        Assert.Equal(0, score.StripEnergy);
    }

    [Fact]
    public void A_shape_crossing_the_cut_line_is_flagged()
    {
        var score = CutLineCheck.Evaluate(Image(p => Shape(p, 700, 1100, 1700, 1850)), Crop);

        Assert.True(score.Flagged);
        Assert.True(score.Score >= CutLineCheck.FlagRatio);
    }

    [Fact]
    public void A_shape_that_ends_above_the_cut_line_is_not_flagged()
    {
        var score = CutLineCheck.Evaluate(Image(p => Shape(p, 700, 1100, 1700, 1780)), Crop);

        Assert.False(score.Flagged);
    }

    [Fact]
    public void A_busy_band_below_the_cut_does_not_flag_detail_that_matches_it()
    {
        var score = CutLineCheck.Evaluate(Image(p => Stripes(p, 1810, Height)), Crop);

        Assert.False(score.Flagged);
    }

    [Fact]
    public void A_picture_kept_to_the_bottom_has_nothing_below_the_cut()
    {
        var crop = KdpPictureCrop.For(Width, Height, 5000).Source;

        Assert.Equal(CutLineScore.None, CutLineCheck.Evaluate(Image(p => Shape(p, 0, 0, Width, Height)), crop));
    }

    [Fact]
    public void A_picture_with_too_thin_a_band_under_the_strip_is_not_flagged()
    {
        const int height = 1880;
        var pixels = new byte[Width * height];
        Array.Fill(pixels, (byte)235);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                pixels[y * Width + x] = (byte)(((x / 6) + (y / 6)) % 2 == 0 ? 40 : 160);
            }
        }

        var crop = KdpPictureCrop.For(Width, height).Source;

        Assert.Equal(CutLineScore.None, CutLineCheck.Evaluate(new LumaImage(Width, height, pixels), crop));
    }

    [Fact]
    public void Textured_art_that_runs_down_the_whole_picture_is_not_flagged()
    {
        var score = CutLineCheck.Evaluate(Image(p => Stripes(p, 0, Height)), Crop);

        Assert.False(score.Flagged);
        Assert.True(score.Score < CutLineCheck.FlagRatio);
    }

    [Fact]
    public void A_narrow_character_is_not_averaged_away()
    {
        var score = CutLineCheck.Evaluate(Image(p => Shape(p, 800, 1500, 900, 1900)), Crop);

        Assert.True(score.Flagged);
    }

    private static LumaImage Image(Action<byte[]> draw)
    {
        var pixels = new byte[Width * Height];
        Array.Fill(pixels, (byte)235);
        draw(pixels);
        return new LumaImage(Width, Height, pixels);
    }

    /// <summary>A dark box with a lighter inside, so it has edges and texture like drawn art.</summary>
    private static void Shape(byte[] pixels, int left, int top, int right, int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                pixels[y * Width + x] = (byte)(((x / 6) + (y / 6)) % 2 == 0 ? 40 : 160);
            }
        }
    }

    private static void Stripes(byte[] pixels, int top, int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                pixels[y * Width + x] = (byte)(((x / 6) + (y / 6)) % 2 == 0 ? 40 : 160);
            }
        }
    }
}
