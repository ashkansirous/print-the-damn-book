namespace PrintTheDamnBook.Core.Kdp;

/// <summary>A square of pixels in a picture: the part the title page's round picture is cut from.</summary>
public readonly record struct KdpSquare(int X, int Y, int Side);

/// <summary>
/// The round title-page picture (Slice 46 item G, <c>docs/design/kdp-typography.md</c>): 230 pt across, 6 pt white border, soft shadow. Where it is cut from is the
/// <c>styles.titlePicture</c> choice (the centre as fractions 0 to 1 of the picture's width and height from its top-left corner, so a portrait picture's centre never leaves
/// the range <see cref="KdpTitlePicture"/> accepts; the diameter as a fraction of the picture's shorter side); with none saved it is the
/// first story picture, centred, as wide as its shorter side (decision 12).
/// </summary>
public static class KdpTitleCircle
{
    public const double DiameterPt = 230;
    public const double BorderPt = 6;
    public const double GapToTitlePt = 28;

    /// <summary>The soft shadow of the mock: 0 2 6, 12% black (offset y, blur, opacity).</summary>
    public const double ShadowOffsetPt = 2;

    public const double ShadowBlurPt = 6;
    public const double ShadowOpacity = 0.12;

    /// <summary>The page of the story picture the circle is cut from (1-based): the saved one when it exists in the book, else the first.</summary>
    public static int PageFor(KdpTitlePicture? saved, int pictureCount)
    {
        return saved is { } s && s.Page >= 1 && s.Page <= pictureCount ? s.Page : 1;
    }

    /// <summary>The square of a <paramref name="width"/> x <paramref name="height"/> picture the circle is cut from, always inside the picture.</summary>
    public static KdpSquare Square(int width, int height, KdpTitlePicture? saved, int pictureCount)
    {
        var shorter = Math.Min(width, height);
        if (saved is null || saved.Page > pictureCount)
        {
            return new KdpSquare((width - shorter) / 2, (height - shorter) / 2, shorter);
        }

        var side = Math.Clamp((int)Math.Round(saved.Diameter * shorter), 1, shorter);
        var x = Math.Clamp((int)Math.Round(saved.CenterX * width - side / 2.0), 0, width - side);
        var y = Math.Clamp((int)Math.Round(saved.CenterY * height - side / 2.0), 0, height - side);
        return new KdpSquare(x, y, side);
    }
}
