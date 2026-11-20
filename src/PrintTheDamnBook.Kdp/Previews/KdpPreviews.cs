using System.Globalization;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp.Previews;

/// <summary>The files <c>--previews</c> wrote (paths under the output folder).</summary>
public sealed record KdpPreviewFiles(string ContactSheetPath, string PagesFolder, int PageCount);

/// <summary>
/// <c>--previews</c> (Slice 30): small per-page PNGs of the finished interior (the same drawing as <c>interior.pdf</c>, so words, numbers and
/// crops are what the owner uploads) and one contact sheet in spread order, for the visual check. Never full size.
/// </summary>
public static class KdpPreviews
{
    public const string ContactSheetName = "contact-sheet.png";
    public const string PagesFolderName = "previews/interior";

    /// <summary>The path of a page's preview relative to the output folder; the argument is the page position from 1.</summary>
    public const string PagePathFormat = PagesFolderName + "/page-{0:00}.png";

    private const int LabelFontSize = 12;

    /// <summary>Renders the previews of an interior that was just built from <paramref name="input"/> and writes them under <paramref name="outFolder"/>.</summary>
    public static KdpPreviewFiles Write(string outFolder, KdpBookInput input, KdpInteriorResult result)
    {
        var pages = RenderPages(input, result);
        var sheetPath = Path.Combine(outFolder, ContactSheetName);
        var sheet = ContactSheet(pages);
        var folder = Path.Combine(outFolder, PagesFolderName);
        ReplacePages(folder, pages);
        File.WriteAllBytes(sheetPath, sheet);
        return new KdpPreviewFiles(sheetPath, folder, pages.Count);
    }

    /// <summary>A run without <c>--previews</c> removes those of an earlier run, so none sits beside a newer interior.pdf.</summary>
    public static void Remove(string outFolder)
    {
        var sheetPath = Path.Combine(outFolder, ContactSheetName);
        if (File.Exists(sheetPath))
        {
            File.Delete(sheetPath);
        }

        var folder = Path.Combine(outFolder, PagesFolderName);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static IReadOnlyList<byte[]> RenderPages(KdpBookInput input, KdpInteriorResult result)
    {
        var drawing = KdpInterior.DrawingFor(input, result.Plan, result.Pictures, result.Stories);
        var pages = KdpInteriorDocument.RenderPagePreviews(drawing);
        return pages.Count == result.Plan.PageCount ? pages : throw new KdpException($"The previews have {pages.Count} pages, but the interior has {result.Plan.PageCount}.");
    }

    /// <summary>Previews of an earlier run are removed first so none is stale.</summary>
    private static void ReplacePages(string folder, IReadOnlyList<byte[]> pages)
    {
        if (Directory.Exists(folder))
        {
            foreach (var old in Directory.GetFiles(folder, "page-*.png"))
            {
                File.Delete(old);
            }
        }

        Directory.CreateDirectory(folder);
        for (var i = 0; i < pages.Count; i++)
        {
            File.WriteAllBytes(Path.Combine(folder, string.Format(CultureInfo.InvariantCulture, "page-{0:00}.png", i + 1)), pages[i]);
        }
    }

    private static byte[] ContactSheet(IReadOnlyList<byte[]> pages)
    {
        var layout = KdpContactSheetLayout.For(pages.Count);
        using var sheet = new SKBitmap(layout.SheetWidth, layout.SheetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(0xD8, 0xD8, 0xD8));
        using var font = new SKFont { Size = LabelFontSize };
        using var ink = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var outline = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        for (var position = 1; position <= pages.Count; position++)
        {
            DrawPage(canvas, pages[position - 1], layout.PageBox(position), outline);
            var label = layout.LabelBox(position);
            canvas.DrawText($"p{position}", label.X + 2, label.Y + label.Height - 4, SKTextAlign.Left, font, ink);
        }

        using var image = SKImage.FromBitmap(sheet);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100) ?? throw new KdpException("The contact sheet could not be encoded.");
        return encoded.ToArray();
    }

    private static void DrawPage(SKCanvas canvas, byte[] png, PixelBox box, SKPaint outline)
    {
        using var page = SKBitmap.Decode(png) ?? throw new KdpException("A page preview could not be decoded.");
        using var image = SKImage.FromBitmap(page);
        var target = new SKRect(box.X, box.Y, box.X + box.Width, box.Y + box.Height);
        canvas.DrawImage(image, target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), null);
        canvas.DrawRect(target, outline);
    }
}
