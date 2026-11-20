using PrintTheDamnBook.Core.Text;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp.Cover;

/// <summary>
/// The soft shadow under cover words (Slice 37): the words are set once in the shadow colour on a transparent page of their own box (the same layout code as the
/// real words, so every letter lands in the same place), thickened and blurred with SkiaSharp, and placed as one transparent PNG beneath the real vector text.
/// The text itself is drawn once, never as stacked offset copies. Skia is already a dependency of the Kdp project and runs on Linux.
/// </summary>
internal static class KdpSoftShadow
{
    /// <summary>How far the shadow image reaches beyond the words' box on every side, so the blur is not cut off.</summary>
    public const double PadInches = 0.15;

    private const int Dpi = 300;
    private const float Strength = 2.4f;
    private const float DilatePxPerPt = 0.05f;
    private const float SigmaBasePx = 2f;
    private const float SigmaPxPerPt = 0.15f;
    private const float DropPx = 3f;
    private const float LineHeight = 1.3f;

    /// <summary>The shadow of the words <paramref name="draw"/> sets in a box of <paramref name="widthInches"/> by <paramref name="heightInches"/>, as a PNG that covers the box plus <see cref="PadInches"/> on every side.</summary>
    public static byte[]? Png(double widthInches, double heightInches, double pt, Action<IContainer, string> draw, string shadowColor)
    {
        var words = Words(widthInches, heightInches, draw, shadowColor);
        return words is null ? null : Blur(words, (float)pt);
    }

    private static byte[]? Words(double widthInches, double heightInches, Action<IContainer, string> draw, string color)
    {
        try
        {
            return Render(widthInches, heightInches, draw, color);
        }
        catch (DocumentLayoutException)
        {
            // Words that overflow the box cannot be laid out in it. A looser layout would align them differently from the real words (the shadow would sit off the
            // letters), so there is no shadow; the caller reports that in a warning.
            return null;
        }
    }

    private static byte[]? Render(double widthInches, double heightInches, Action<IContainer, string> draw, string color)
    {
        var document = Document.Create(container => container.Page(page =>
        {
            page.Size((float)widthInches, (float)heightInches, Unit.Inch);
            page.Margin(0);
            page.DefaultTextStyle(s => s.FontFamily(StoryFont.Andika).LineHeight(LineHeight));
            // The same exact-size box the real words get (KdpInteriorDocument.InBox), so AlignMiddle and the like place the shadow words exactly where the letters are.
            page.Content().Width((float)widthInches, Unit.Inch).Height((float)heightInches, Unit.Inch).Element(words => draw(words, color));
        }));
        var settings = new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = Dpi, UseTransparentBackground = true };
        return document.GenerateImages(settings).FirstOrDefault();
    }

    private static byte[] Blur(byte[] wordsPng, float pt)
    {
        using var words = SKBitmap.Decode(wordsPng);
        var pad = (int)Math.Round(PadInches * Dpi);
        var info = new SKImageInfo(words.Width + 2 * pad, words.Height + 2 * pad, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var blurred = Soften(words, info, pad, pt);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);
        using var strengthen = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(AlphaTimes(Strength)) };
        surface.Canvas.DrawImage(blurred, 0, 0, new SKSamplingOptions(), strengthen);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>The words thickened a little (small words would otherwise blur away) and blurred, nudged down so the shadow falls a touch below the letters.</summary>
    private static SKImage Soften(SKBitmap words, SKImageInfo info, int pad, float pt)
    {
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);
        var dilate = Math.Max(1f, MathF.Round(pt * DilatePxPerPt));
        using var thick = SKImageFilter.CreateDilate(dilate, dilate);
        var sigma = SigmaBasePx + pt * SigmaPxPerPt;
        using var soft = SKImageFilter.CreateBlur(sigma, sigma, thick);
        using var paint = new SKPaint { ImageFilter = soft };
        using var image = SKImage.FromBitmap(words);
        surface.Canvas.DrawImage(image, pad, pad + DropPx, new SKSamplingOptions(), paint);
        return surface.Snapshot();
    }

    private static float[] AlphaTimes(float factor) =>
    [
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, factor, 0,
    ];
}
