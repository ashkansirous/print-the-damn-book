using PrintTheDamnBook.Core.Kdp;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp.CropSheet;

/// <summary>One picture of the crop sheet: its key (<see cref="KdpPictureKeys"/>), its encoded bytes as the book holds them and the offset in use.</summary>
public sealed record KdpCropSheetPicture(string Key, byte[] Encoded, int OffsetPx);

/// <summary>The files <c>--crop-sheet</c> wrote (paths under the output folder).</summary>
public sealed record KdpCropSheetFiles(string SheetPath, string FolderPath, int Count);

/// <summary>
/// <c>--crop-sheet</c> (Slice 36): <c>crop/sheet.png</c>, every picture of the book whole and uncut at about 200 px wide, the kept window outlined and
/// the rest dimmed, labelled by key; and <c>crop/&lt;key&gt;.png</c>, the same picture 480 px wide. The crop agent decides each window from these, so it
/// never sees a full-size picture. Never written without the option; an earlier run's files are removed so none is stale.
/// </summary>
public static class KdpCropSheetWriter
{
    private const int LabelFontSize = 12;
    private const float OutlineWidth = 2;
    private const byte DimAlpha = 120;

    public static KdpCropSheetFiles Write(string outFolder, IReadOnlyList<KdpCropSheetPicture> pictures)
    {
        var folder = Path.Combine(outFolder, KdpCropSheetLayout.FolderName);
        Remove(outFolder);
        Directory.CreateDirectory(folder);
        var layout = KdpCropSheetLayout.For(pictures.Count);
        using var sheet = new SKBitmap(layout.SheetWidth, layout.SheetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(0xD8, 0xD8, 0xD8));
        using var font = new SKFont { Size = LabelFontSize };
        using var ink = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        for (var i = 0; i < pictures.Count; i++)
        {
            using var picture = KdpImageDecoder.DecodeUpright(pictures[i].Encoded);
            var small = KdpCropOutline.For(picture.Width, picture.Height, pictures[i].OffsetPx, KdpCropSheetLayout.CellWidthPx, KdpCropSheetLayout.CellHeightPx);
            var cell = layout.CellBox(i);
            DrawOutlined(canvas, picture, small, cell.X + (cell.Width - small.ImageWidth) / 2, cell.Y);
            var label = layout.LabelBox(i);
            canvas.DrawText(KdpCropSheetLayout.Label(pictures[i].Key, small.AppliedOffsetPx, small.MaxOffsetPx), label.X + 2, label.Y + label.Height - 4, SKTextAlign.Left, font, ink);
            WriteDetail(outFolder, pictures[i].Key, picture, pictures[i].OffsetPx);
        }

        var sheetPath = Path.Combine(folder, KdpCropSheetLayout.SheetName);
        File.WriteAllBytes(sheetPath, EncodePng(sheet));
        return new KdpCropSheetFiles(sheetPath, folder, pictures.Count);
    }

    /// <summary>Removes the sheet and the larger copies of an earlier run.</summary>
    public static void Remove(string outFolder)
    {
        var folder = Path.Combine(outFolder, KdpCropSheetLayout.FolderName);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void WriteDetail(string outFolder, string key, SKBitmap picture, int offsetPx)
    {
        var outline = KdpCropOutline.For(picture.Width, picture.Height, offsetPx, KdpCropSheetLayout.DetailWidthPx, KdpCropSheetLayout.DetailMaxHeightPx);
        var strip = KdpCropSheetLayout.DetailLabelHeightPx;
        using var detail = new SKBitmap(outline.ImageWidth, outline.ImageHeight + strip, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(detail);
        canvas.Clear(new SKColor(0xD8, 0xD8, 0xD8));
        DrawOutlined(canvas, picture, outline, 0, strip);
        using var font = new SKFont { Size = LabelFontSize + 2 };
        using var ink = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawText(KdpCropSheetLayout.Label(key, outline.AppliedOffsetPx, outline.MaxOffsetPx), 4, strip - 5, SKTextAlign.Left, font, ink);
        File.WriteAllBytes(Path.Combine(outFolder, KdpCropSheetLayout.DetailPath(key)), EncodePng(detail));
    }

    /// <summary>The whole picture at <paramref name="outline"/>'s size from (<paramref name="x"/>, <paramref name="y"/>), the part the crop drops dimmed and the kept window outlined in red.</summary>
    private static void DrawOutlined(SKCanvas canvas, SKBitmap picture, KdpCropOutline outline, int x, int y)
    {
        using var image = SKImage.FromBitmap(picture);
        var target = new SKRect(x, y, x + outline.ImageWidth, y + outline.ImageHeight);
        canvas.DrawImage(image, target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), null);
        var window = new SKRect(x + outline.Window.X, y + outline.Window.Y, x + outline.Window.X + outline.Window.Width, y + outline.Window.Y + outline.Window.Height);
        using var dim = new SKPaint { Color = new SKColor(0, 0, 0, DimAlpha) };
        canvas.DrawRect(new SKRect(target.Left, target.Top, target.Right, window.Top), dim);
        canvas.DrawRect(new SKRect(target.Left, window.Bottom, target.Right, target.Bottom), dim);
        canvas.DrawRect(new SKRect(target.Left, window.Top, window.Left, window.Bottom), dim);
        canvas.DrawRect(new SKRect(window.Right, window.Top, target.Right, window.Bottom), dim);
        using var line = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = OutlineWidth };
        canvas.DrawRect(window, line);
    }

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100) ?? throw new KdpException("A crop sheet picture could not be encoded.");
        return encoded.ToArray();
    }
}
