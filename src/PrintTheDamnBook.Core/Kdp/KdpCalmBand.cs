using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>
/// Where the cover title goes (Slice 36): the calmest of a few candidate bands of the front picture, so white letters with a shadow stay readable
/// and cover no character. Calm means the least spread in brightness. Pure maths on a small brightness grid; the Kdp project makes the grid.
/// </summary>
public static class KdpCalmBand
{
    /// <summary>How far from the trim's top and bottom the first and last candidate sit.</summary>
    public const double EdgeInches = 0.5;

    /// <summary>Above this mean brightness (of 255) white letters with a dark shadow are weak (Freddy's pale back cover measured 218 to 239 under its words), so the words use dark ink with a white halo instead.</summary>
    public const double LightBackdropMean = 170;

    private const int CandidateCount = 4;

    /// <summary>The candidate bands, top to bottom, each <paramref name="bandHeight"/> tall and <paramref name="bandWidth"/> wide, inside a trim box of <paramref name="trim"/> (inches).</summary>
    public static IReadOnlyList<RectD> Candidates(RectD trim, double bandWidth, double bandHeight)
    {
        var x = trim.X + (trim.Width - bandWidth) / 2;
        var first = trim.Y + EdgeInches;
        var last = trim.Y + trim.Height - EdgeInches - bandHeight;
        return Enumerable.Range(0, CandidateCount)
            .Select(i => new RectD(x, first + (last - first) * i / (CandidateCount - 1), bandWidth, bandHeight))
            .ToList();
    }

    /// <summary>
    /// The index of the calmest band. <paramref name="luma"/> is the picture's brightness grid; <paramref name="pictureOrigin"/> and
    /// <paramref name="pictureSize"/> say which part of the page (inches) the grid covers. A tie goes to the earlier (higher) band.
    /// </summary>
    public static int Choose(IReadOnlyList<RectD> bands, LumaImage luma, (double X, double Y) pictureOrigin, (double Width, double Height) pictureSize)
    {
        if (bands.Count == 0)
        {
            throw new ArgumentException("At least one band is needed.", nameof(bands));
        }

        var best = 0;
        var bestSpread = double.MaxValue;
        for (var i = 0; i < bands.Count; i++)
        {
            var spread = Spread(luma, bands[i], pictureOrigin, pictureSize);
            if (spread < bestSpread)
            {
                best = i;
                bestSpread = spread;
            }
        }

        return best;
    }

    /// <summary>The standard deviation of the brightness inside the band (255 for a band that holds no pixel, so it is never chosen over a real one).</summary>
    public static double Spread(LumaImage luma, RectD band, (double X, double Y) pictureOrigin, (double Width, double Height) pictureSize)
    {
        return Measure(luma, band, pictureOrigin, pictureSize).Spread;
    }

    /// <summary>The mean brightness (0 to 255) inside the band; 0 for a band that holds no pixel.</summary>
    public static double Brightness(LumaImage luma, RectD band, (double X, double Y) pictureOrigin, (double Width, double Height) pictureSize)
    {
        return Measure(luma, band, pictureOrigin, pictureSize).Mean;
    }

    /// <summary>True when white letters would be hard to read on a band this bright (see <see cref="LightBackdropMean"/>).</summary>
    public static bool IsLight(double meanBrightness) => meanBrightness >= LightBackdropMean;

    /// <summary>The opacity (0 to 255) of each dark shadow copy behind white letters: faint on a dark band, strong on a light one so the white letters still read.</summary>
    public static byte ShadowAlpha(double meanBrightness) => IsLight(meanBrightness) ? StrongShadowAlpha : NormalShadowAlpha;

    /// <summary>The shadow opacity on a dark or middling band.</summary>
    public const byte NormalShadowAlpha = 0x55;

    /// <summary>The shadow opacity on a light band (see <see cref="IsLight"/>).</summary>
    public const byte StrongShadowAlpha = 0xC0;

    private static (double Mean, double Spread) Measure(LumaImage luma, RectD band, (double X, double Y) pictureOrigin, (double Width, double Height) pictureSize)
    {
        var left = Cell(band.X - pictureOrigin.X, pictureSize.Width, luma.Width);
        var right = Cell(band.X + band.Width - pictureOrigin.X, pictureSize.Width, luma.Width);
        var top = Cell(band.Y - pictureOrigin.Y, pictureSize.Height, luma.Height);
        var bottom = Cell(band.Y + band.Height - pictureOrigin.Y, pictureSize.Height, luma.Height);
        double sum = 0;
        double squares = 0;
        var count = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                double value = luma.Pixels[y * luma.Width + x];
                sum += value;
                squares += value * value;
                count++;
            }
        }

        if (count == 0)
        {
            return (0, byte.MaxValue);
        }

        var mean = sum / count;
        return (mean, Math.Sqrt(Math.Max(0, squares / count - mean * mean)));
    }

    private static int Cell(double inches, double extent, int cells) => Math.Clamp((int)Math.Round(inches / extent * cells, MidpointRounding.AwayFromZero), 0, cells);
}
