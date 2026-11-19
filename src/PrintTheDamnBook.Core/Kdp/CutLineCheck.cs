namespace PrintTheDamnBook.Core.Kdp;

/// <summary>A picture's brightness, one byte per pixel, row by row (what <see cref="CutLineCheck"/> reads; the Kdp project makes it from the decoded picture).</summary>
public sealed record LumaImage(int Width, int Height, byte[] Pixels);

/// <summary>What the check found: the strongest local detail just below the cut, the detail of the band below it, their ratio, and whether the page is flagged.</summary>
public sealed record CutLineScore(double StripEnergy, double BandEnergy, double Score, bool Flagged)
{
    public static readonly CutLineScore None = new(0, 0, 0, false);
}

/// <summary>
/// Would a character be cut by the page crop (Slice 28)? The kept part ends at the cut line; the picture's own text band lies below it and is
/// plain, so detail (edge energy) in the strip just below the cut line, much stronger than the band under it, is art running across the cut.
/// Pure maths on pixels, split into column blocks so a character a tenth of the width wide is not averaged away.
/// </summary>
public static class CutLineCheck
{
    /// <summary>The strip below the cut is this fraction of the picture's height (at least <see cref="MinStripRows"/> rows).</summary>
    public const double StripFraction = 0.03;

    public const int MinStripRows = 4;

    /// <summary>The band under the strip needs this many rows to be compared with it; a thinner band means no check (it would read as plain and flag any textured art).</summary>
    public const int MinBandRows = 8;

    public const int Blocks = 16;

    /// <summary>A block needs at least this mean edge energy (brightness steps per pixel, 0-510) in the strip to count as detail, not paper grain.</summary>
    public const double MinStripEnergy = 6;

    /// <summary>Added to the band's energy so a perfectly plain band does not make every speck an infinite ratio.</summary>
    public const double BaselineEnergy = 2;

    /// <summary>The strip's detail must be at least this many times the band's (plus <see cref="BaselineEnergy"/>) to flag the page.</summary>
    public const double FlagRatio = 2.5;

    public static CutLineScore Evaluate(LumaImage image, PixelRect crop)
    {
        var strip = Math.Max(MinStripRows, (int)Math.Round(image.Height * StripFraction));
        var stripTop = crop.Y + crop.Height;
        var stripBottom = Math.Min(image.Height, stripTop + strip);
        if (stripBottom - stripTop < 2 || image.Height - stripBottom < MinBandRows || image.Width < Blocks)
        {
            return CutLineScore.None;
        }

        var best = CutLineScore.None;
        for (var block = 0; block < Blocks; block++)
        {
            var left = image.Width * block / Blocks;
            var right = image.Width * (block + 1) / Blocks;
            var stripEnergy = Energy(image, left, right, stripTop, stripBottom);
            var bandEnergy = Energy(image, left, right, stripBottom, image.Height);
            var score = stripEnergy / (bandEnergy + BaselineEnergy);
            var flagged = stripEnergy >= MinStripEnergy && score >= FlagRatio;
            if (flagged && !best.Flagged || flagged == best.Flagged && score > best.Score)
            {
                best = new CutLineScore(stripEnergy, bandEnergy, score, flagged);
            }
        }

        return best;
    }

    /// <summary>Mean of |step right| + |step down| over the pixels of the rows and columns (0 when the region has no neighbours to compare).</summary>
    private static double Energy(LumaImage image, int left, int right, int top, int bottom)
    {
        if (bottom - top < 2 || right - left < 2)
        {
            return 0;
        }

        long total = 0;
        for (var y = top; y < bottom - 1; y++)
        {
            var row = y * image.Width;
            var below = row + image.Width;
            for (var x = left; x < right - 1; x++)
            {
                var here = image.Pixels[row + x];
                total += Math.Abs(here - image.Pixels[row + x + 1]) + Math.Abs(here - image.Pixels[below + x]);
            }
        }

        return (double)total / ((bottom - top - 1) * (right - left - 1));
    }
}
