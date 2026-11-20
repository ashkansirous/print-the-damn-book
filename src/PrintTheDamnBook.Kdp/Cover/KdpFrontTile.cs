using PrintTheDamnBook.Core.Kdp;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp.Cover;

/// <summary>
/// The front cover of a book as a small picture (Slice 41), for the "More books" block of another book's last page: the real cover drawing (crop, title box,
/// colours and soft shadow as that book's own cover has them) rasterised at a low resolution and cut to the front trim. The tile is shown at about 1.3 in, so
/// <see cref="Dpi"/> is far more than it needs.
/// </summary>
internal static class KdpFrontTile
{
    public const int Dpi = 96;
    private const int JpegQuality = 90;

    public static byte[] Jpeg(KdpCoverInput input)
    {
        var (drawing, _, _, _) = KdpCover.Prepare(input);
        var settings = new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = Dpi };
        var page = KdpCoverDocument.Create(drawing).GenerateImages(settings).First();
        using var cover = SKBitmap.Decode(page) ?? throw new KdpException("The cover drawing could not be decoded for the small front picture.");
        var scale = cover.Width / KdpCoverSize.CoverWidth(input.PageCount);
        var trim = KdpCoverSize.FrontTrim(input.PageCount, input.Direction);
        var area = SKRectI.Create((int)Math.Round(trim.X * scale), (int)Math.Round(trim.Y * scale), (int)Math.Round(trim.Width * scale), (int)Math.Round(trim.Height * scale));
        using var tile = new SKBitmap(area.Width, area.Height);
        using var canvas = new SKCanvas(tile);
        using var whole = SKImage.FromBitmap(cover);
        canvas.DrawImage(whole, SKRect.Create(area.Left, area.Top, area.Width, area.Height), SKRect.Create(area.Width, area.Height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var image = SKImage.FromBitmap(tile);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return data.ToArray();
    }
}
