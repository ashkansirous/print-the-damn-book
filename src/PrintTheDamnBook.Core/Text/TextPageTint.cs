namespace PrintTheDamnBook.Core.Text;

/// <summary>The paper of the text pages: plain white, or tinted with the cover's average colour.</summary>
public enum TextPageBackground
{
    White,
    TintedFromCover,
}

/// <summary>
/// The text-page tint: the cover's average colour lifted to OKLCH lightness of at least <see cref="MinLightness"/>, so dark
/// text stays easy to read on it. The chroma is kept as far as the lifted colour still fits in sRGB.
/// </summary>
public static class TextPageTint
{
    public const double MinLightness = 0.96;

    /// <summary>Lightness really aimed for, a little above the minimum so rounding to bytes can never land below it.</summary>
    private const double TargetLightness = MinLightness + 0.002;

    private const int BytesPerPixel = 4;
    private const byte MinOpaqueAlpha = 8;
    private const int GamutSteps = 24;

    /// <summary>
    /// The tint for a cover given as straight (not premultiplied) BGRA pixels, or null when no pixel is visible.
    /// <paramref name="stride"/> is the number of bytes per row.
    /// </summary>
    public static Rgb? FromBgra(ReadOnlySpan<byte> pixels, int width, int height, int stride)
    {
        double red = 0, green = 0, blue = 0;
        var count = 0;
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var at = row + x * BytesPerPixel;
                if (at + BytesPerPixel > pixels.Length || pixels[at + 3] < MinOpaqueAlpha)
                {
                    continue;
                }

                red += ToLinear(pixels[at + 2]);
                green += ToLinear(pixels[at + 1]);
                blue += ToLinear(pixels[at]);
                count++;
            }
        }

        return count == 0 ? null : Lift(red / count, green / count, blue / count);
    }

    /// <summary>The OKLCH lightness (0 to 1) of an sRGB colour.</summary>
    public static double LightnessOf(Rgb colour)
    {
        return ToOklab(ToLinear(colour.R), ToLinear(colour.G), ToLinear(colour.B)).L;
    }

    private static Rgb Lift(double r, double g, double b)
    {
        var (l, a, bb) = ToOklab(r, g, b);
        var lifted = Math.Max(l, TargetLightness);

        // Keep as much of the colour's chroma as still fits in sRGB at the lifted lightness (zero chroma, a grey, always fits).
        var low = 0.0;
        var high = 1.0;
        for (var i = 0; i < GamutSteps; i++)
        {
            var mid = (low + high) / 2;
            if (FitsSrgb(lifted, a * mid, bb * mid))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        var (lr, lg, lb) = FromOklab(lifted, a * low, bb * low);
        return new Rgb(ToByte(lr), ToByte(lg), ToByte(lb));
    }

    private static bool FitsSrgb(double l, double a, double b)
    {
        var (r, g, bl) = FromOklab(l, a, b);
        return r is >= 0 and <= 1 && g is >= 0 and <= 1 && bl is >= 0 and <= 1;
    }

    private static (double L, double A, double B) ToOklab(double r, double g, double b)
    {
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static (double R, double G, double B) FromOklab(double lightness, double a, double b)
    {
        var l = Cube(lightness + 0.3963377774 * a + 0.2158037573 * b);
        var m = Cube(lightness - 0.1055613458 * a - 0.0638541728 * b);
        var s = Cube(lightness - 0.0894841775 * a - 1.2914855480 * b);
        return (
            4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
    }

    private static double Cube(double value) => value * value * value;

    private static double ToLinear(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static byte ToByte(double linear)
    {
        var c = Math.Clamp(linear, 0, 1);
        var encoded = c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        return (byte)Math.Round(encoded * 255);
    }
}
