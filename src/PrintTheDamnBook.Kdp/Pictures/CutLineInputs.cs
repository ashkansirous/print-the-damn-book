using PrintTheDamnBook.Core.Kdp;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp.Pictures;

/// <summary>What the Kdp project hands Core's <see cref="CutLineCheck"/> and the report: the picture's brightness, and a small preview of a flagged cut.</summary>
public static class CutLineInputs
{
    public const int PreviewWidthPx = 480;

    private const int PreviewJpegQuality = 80;

    /// <summary>How much of the picture below the cut the preview shows, as a fraction of its height.</summary>
    private const double PreviewBelowFraction = 0.06;

    /// <summary>The upright, opaque picture (drawn on white by the decoder) as one brightness byte per pixel.</summary>
    public static LumaImage Luma(SKBitmap upright)
    {
        var luma = new byte[upright.Width * upright.Height];
        var pixels = upright.GetPixelSpan();
        var rowBytes = upright.RowBytes;
        for (var y = 0; y < upright.Height; y++)
        {
            var row = y * rowBytes;
            for (var x = 0; x < upright.Width; x++)
            {
                var p = row + x * 4;
                luma[y * upright.Width + x] = (byte)((77 * pixels[p] + 150 * pixels[p + 1] + 29 * pixels[p + 2]) >> 8);
            }
        }

        return new LumaImage(upright.Width, upright.Height, luma);
    }

    /// <summary>The kept part and a strip below it, small, with a red line on the cut: the picture the owner (or the visual check) looks at.</summary>
    public static byte[] Preview(SKBitmap upright, PixelRect crop)
    {
        var bottom = Math.Min(upright.Height, crop.Y + crop.Height + (int)Math.Round(upright.Height * PreviewBelowFraction));
        var from = new SKRect(crop.X, crop.Y, crop.X + crop.Width, bottom);
        var scale = (double)PreviewWidthPx / crop.Width;
        var height = Math.Max(1, (int)Math.Round((bottom - crop.Y) * scale));
        using var preview = new SKBitmap(PreviewWidthPx, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(preview))
        {
            using var image = SKImage.FromBitmap(upright);
            canvas.DrawImage(image, from, new SKRect(0, 0, PreviewWidthPx, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), null);
            using var line = new SKPaint { Color = SKColors.Red, StrokeWidth = 2, IsAntialias = true };
            var cut = (float)(crop.Height * scale);
            canvas.DrawLine(0, cut, PreviewWidthPx, cut, line);
        }

        using var previewImage = SKImage.FromBitmap(preview);
        using var encoded = previewImage.Encode(SKEncodedImageFormat.Jpeg, PreviewJpegQuality) ?? throw new KdpException("A preview could not be encoded.");
        return encoded.ToArray();
    }

    /// <summary>The file a flagged page's preview is written to, under the output folder.</summary>
    public static string PreviewPath(int storyPage) => $"previews/page-{storyPage:00}.jpg";
}
