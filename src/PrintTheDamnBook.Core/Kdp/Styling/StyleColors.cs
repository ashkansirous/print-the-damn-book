using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>Colours of the styled text: hex conversion, WCAG contrast and darkening a colour (in OKLab lightness, like <see cref="TextPageTint"/>) until it reads on its page.</summary>
public static class StyleColors
{
    public const string Ink = "#2B2540";

    private const int DarkenSteps = 100;

    public static string ToHex(Rgb c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Rgb Parse(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length != 6)
        {
            throw new ArgumentException($"'{hex}' is not a #RRGGBB colour.", nameof(hex));
        }

        return new Rgb(Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16));
    }

    /// <summary>WCAG 2 contrast ratio, 1 to 21.</summary>
    public static double Contrast(Rgb a, Rgb b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary><paramref name="colour"/> itself when it already has <paramref name="minContrast"/> on <paramref name="background"/>, else the first darker shade (same hue, lightness lowered in 1% steps) that has.</summary>
    public static Rgb DarkenToContrast(Rgb colour, Rgb background, double minContrast = StyleSpec.MinContrast)
    {
        if (Contrast(colour, background) >= minContrast)
        {
            return colour;
        }

        var (l, a, b) = Oklab.From(colour);
        for (var step = 1; step <= DarkenSteps; step++)
        {
            var candidate = Oklab.ToRgb(l * (1 - step / (double)DarkenSteps), a, b);
            if (Contrast(candidate, background) >= minContrast)
            {
                return candidate;
            }
        }

        return new Rgb(0, 0, 0);
    }

    private static double Luminance(Rgb c) => 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);

    private static double Linear(byte channel)
    {
        var v = channel / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}

/// <summary>sRGB to OKLab and back (the same matrices as <see cref="TextPageTint"/>), out-of-gamut results clamped.</summary>
internal static class Oklab
{
    public static (double L, double A, double B) From(Rgb c)
    {
        var r = Linear(c.R);
        var g = Linear(c.G);
        var b = Linear(c.B);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    public static Rgb ToRgb(double lightness, double a, double b)
    {
        var l = Cube(lightness + 0.3963377774 * a + 0.2158037573 * b);
        var m = Cube(lightness - 0.1055613458 * a - 0.0638541728 * b);
        var s = Cube(lightness - 0.0894841775 * a - 1.2914855480 * b);
        return new Rgb(
            Encode(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s),
            Encode(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s),
            Encode(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s));
    }

    private static double Cube(double v) => v * v * v;

    private static double Linear(byte channel)
    {
        var v = channel / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static byte Encode(double linear)
    {
        var c = Math.Clamp(linear, 0, 1);
        var encoded = c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        return (byte)Math.Round(encoded * 255);
    }
}
