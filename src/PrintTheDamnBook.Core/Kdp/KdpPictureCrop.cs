namespace PrintTheDamnBook.Core.Kdp;

/// <summary>A rectangle of pixels.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// <summary>
/// How a Dino picture becomes a KDP page: <see cref="Source"/> is the part of the picture kept, <see cref="TargetWidth"/> x
/// <see cref="TargetHeight"/> the pixels it is resampled to, <see cref="EffectiveDpi"/> the resolution the kept part has on the page.
/// </summary>
public sealed record KdpCrop(PixelRect Source, int TargetWidth, int TargetHeight, double EffectiveDpi)
{
    /// <summary>True when the kept part has fewer pixels across than the page needs, so resampling makes it larger.</summary>
    public bool Upscales => Source.Width < TargetWidth;

    /// <summary>How many page pixels one source pixel becomes across (1.45 means a 1.45x enlargement).</summary>
    public double Scale => (double)TargetWidth / Source.Width;
}

/// <summary>The crop of a picture: full width, the bottom (where the picture's own text band is) dropped, the kept part moved down by the page's offset (Slice 28).</summary>
public static class KdpPictureCrop
{
    public static KdpCrop For(int sourceWidth, int sourceHeight, int offsetPx = 0)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "A picture needs a size.");
        }

        var rect = CropRect(sourceWidth, sourceHeight, offsetPx);
        return new KdpCrop(rect, KdpTrim.PictureWidthPx, KdpTrim.PictureHeightPx, rect.Width / KdpTrim.PageWidthInches);
    }

    /// <summary>How far down the kept part can move before it would leave the picture (0 for a picture with no spare height).</summary>
    public static int MaxOffset(int sourceWidth, int sourceHeight)
    {
        var rect = CropRect(sourceWidth, sourceHeight, 0);
        return Math.Max(0, sourceHeight - rect.Height);
    }

    private static PixelRect CropRect(int width, int height, int offsetPx)
    {
        var tallEnough = (long)height * KdpTrim.PictureWidthPx >= (long)width * KdpTrim.PictureHeightPx;
        if (tallEnough)
        {
            var keep = Math.Min(height, (int)Math.Round((double)width * KdpTrim.PictureHeightPx / KdpTrim.PictureWidthPx, MidpointRounding.AwayFromZero));
            return new PixelRect(0, Math.Clamp(offsetPx, 0, height - keep), width, keep);
        }

        var keepWidth = (int)Math.Round((double)height * KdpTrim.PictureWidthPx / KdpTrim.PictureHeightPx, MidpointRounding.AwayFromZero);
        return new PixelRect((width - keepWidth) / 2, 0, keepWidth, height);
    }
}
