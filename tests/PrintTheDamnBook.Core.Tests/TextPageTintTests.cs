using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests;

public class TextPageTintTests
{
    private const int Size = 4;

    private static byte[] Solid(byte r, byte g, byte b, byte a = 255, int pixels = Size * Size)
    {
        var data = new byte[pixels * 4];
        for (var i = 0; i < pixels; i++)
        {
            data[i * 4] = b;
            data[i * 4 + 1] = g;
            data[i * 4 + 2] = r;
            data[i * 4 + 3] = a;
        }

        return data;
    }

    private static Rgb? Tint(byte[] pixels) => TextPageTint.FromBgra(pixels, Size, Size, Size * 4);

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(255, 0, 0)]
    [InlineData(0, 0, 255)]
    [InlineData(20, 90, 40)]
    [InlineData(200, 120, 30)]
    [InlineData(128, 128, 128)]
    [InlineData(255, 255, 255)]
    public void Tint_IsAlwaysLiftedToTheMinimumLightness(byte r, byte g, byte b)
    {
        var tint = Tint(Solid(r, g, b));

        Assert.NotNull(tint);
        Assert.True(TextPageTint.LightnessOf(tint.Value) >= TextPageTint.MinLightness);
    }

    [Fact]
    public void BlackCover_GivesAnAlmostWhiteGrey()
    {
        var tint = Tint(Solid(0, 0, 0))!.Value;

        Assert.True(tint.R > 240 && tint.G > 240 && tint.B > 240);
        Assert.InRange(tint.R - tint.B, -2, 2);
    }

    [Fact]
    public void WhiteCover_StaysWhite()
    {
        Assert.Equal(new Rgb(255, 255, 255), Tint(Solid(255, 255, 255)));
    }

    [Fact]
    public void ColouredCover_KeepsItsHue()
    {
        var red = Tint(Solid(200, 30, 30))!.Value;
        var blue = Tint(Solid(30, 30, 200))!.Value;

        Assert.True(red.R > red.B);
        Assert.True(blue.B > blue.R);
    }

    [Fact]
    public void ColouredCover_IsTintedNotGrey()
    {
        var tint = Tint(Solid(220, 120, 30))!.Value;

        Assert.True(tint.R - tint.B >= 6);
    }

    [Fact]
    public void TransparentPixels_AreIgnored()
    {
        var pixels = Solid(0, 0, 255);
        for (var i = 0; i < 8; i++)
        {
            pixels[i * 4 + 3] = 0;
            pixels[i * 4 + 2] = 255;
            pixels[i * 4] = 0;
        }

        var tint = Tint(pixels)!.Value;

        Assert.True(tint.B > tint.R);
    }

    [Fact]
    public void FullyTransparentOrEmpty_GivesNull()
    {
        Assert.Null(Tint(Solid(10, 10, 10, a: 0)));
        Assert.Null(TextPageTint.FromBgra([], 0, 0, 0));
    }

    [Fact]
    public void Stride_PaddingBytesAreNotRead()
    {
        const int width = 2;
        const int height = 2;
        const int stride = 12;
        var data = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                data[y * stride + x * 4 + 3] = 255;
            }
        }

        var tint = TextPageTint.FromBgra(data, width, height, stride)!.Value;

        Assert.True(TextPageTint.LightnessOf(tint) >= TextPageTint.MinLightness);
    }
}
