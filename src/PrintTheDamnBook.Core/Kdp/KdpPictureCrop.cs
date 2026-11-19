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
}

/// <summary>The plain crop of Slice 27: full width, top kept, the bottom (where the picture's own text band is) dropped.</summary>
public static class KdpPictureCrop
{
    public static KdpCrop For(int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "A picture needs a size.");
        }

        var rect = CropRect(sourceWidth, sourceHeight);
        return new KdpCrop(rect, KdpTrim.PictureWidthPx, KdpTrim.PictureHeightPx, rect.Width / KdpTrim.PageWidthInches);
    }

    private static PixelRect CropRect(int width, int height)
    {
        var tallEnough = (long)height * KdpTrim.PictureWidthPx >= (long)width * KdpTrim.PictureHeightPx;
        if (tallEnough)
        {
            var keep = (int)Math.Round((double)width * KdpTrim.PictureHeightPx / KdpTrim.PictureWidthPx, MidpointRounding.AwayFromZero);
            return new PixelRect(0, 0, width, Math.Min(height, keep));
        }

        var keepWidth = (int)Math.Round((double)height * KdpTrim.PictureWidthPx / KdpTrim.PictureHeightPx, MidpointRounding.AwayFromZero);
        return new PixelRect((width - keepWidth) / 2, 0, keepWidth, height);
    }
}
