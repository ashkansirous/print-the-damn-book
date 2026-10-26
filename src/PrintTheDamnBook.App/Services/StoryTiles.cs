using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PrintTheDamnBook.App.Printing;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.App.Services;

/// <summary>The print options the story text depends on: the same inputs the printer gets, so a tile shows the print. <see cref="Tint"/> is null for white text pages.</summary>
internal readonly record struct StoryContext(PaperKind Kind, double MarginMm, ScalingMode Scaling, PageNumberPosition Numbers, Rgb? Tint)
{
    public SizeD Sheet => Paper.LandscapeSheetMm(Kind);
}

/// <summary>
/// The tiles and fit checks for pages with story text (Slice 14). A tile draws the page at print scale (the same Core rectangles and
/// font sizes the printer uses) and is scaled down by the view, so the Reading order and As printed tiles are the print.
/// </summary>
internal static class StoryTiles
{
    private static readonly Brush PaperBrush = FrozenBrush(Colors.White);

    /// <summary>The size the words get on this page, or null when it has none. <paramref name="imagePixels"/> is the picture's pixel size (needed over a picture).</summary>
    public static TextFitResult? FitOf(BookPage page, SizeD? imagePixels, StoryContext context)
    {
        if (page.Story is not { IsEmpty: false } story)
        {
            return null;
        }

        if (page.HasOwnPageText || page.Kind == PageKind.Text)
        {
            var area = StoryTextLayout.TextPage(context.Sheet, false, context.MarginMm, context.Numbers);
            return StoryPainter.FitPage(story.Text, context.Kind, area.Text);
        }

        return imagePixels is { } size ? StoryPainter.FitBand(story.Text, context.Kind, PlacementOf(size, context).Visible) : null;
    }

    /// <summary>The picture with its band over it, drawn at print scale; null when the page has no band text or the picture is unreadable.</summary>
    public static ImageSource? Picture(BitmapSource? picture, BookPage page, StoryContext context)
    {
        if (picture is null || !page.HasBandText)
        {
            return null;
        }

        var placement = PlacementOf(new SizeD(picture.PixelWidth, picture.PixelHeight), context);
        var band = StoryPainter.PaintBand(page.Story!.Text, context.Kind, placement.Visible);
        var image = placement.Image;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var visible = placement.Visible;
            dc.PushClip(new RectangleGeometry(new Rect(ToDip(visible.X), ToDip(visible.Y), ToDip(visible.Width), ToDip(visible.Height))));
            dc.DrawImage(picture, new Rect(ToDip(image.X), ToDip(image.Y), ToDip(image.Width), ToDip(image.Height)));
            StoryPainter.DrawBand(dc, band);
            dc.Pop();
        }

        return Freeze(visual);
    }

    /// <summary>A text page on the left half of the sheet: its paper (white, or the tint) and the words.</summary>
    public static ImageSource TextPage(BookPage page, StoryContext context)
    {
        var area = StoryTextLayout.TextPage(context.Sheet, false, context.MarginMm, context.Numbers);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var paper = context.Tint is { } tint ? FrozenBrush(Color.FromRgb(tint.R, tint.G, tint.B)) : PaperBrush;
            dc.DrawRectangle(paper, null, new Rect(ToDip(area.Page.X), ToDip(area.Page.Y), ToDip(area.Page.Width), ToDip(area.Page.Height)));
            if (page.Story is { IsEmpty: false } story)
            {
                StoryPainter.DrawTextPage(dc, StoryPainter.PaintTextPage(story.Text, context.Kind, area));
            }
        }

        return Freeze(visual);
    }

    /// <summary>The cover's average colour lifted for text pages, read from its thumbnail; null when it cannot be read.</summary>
    public static Rgb? TintOf(ImageSource? cover)
    {
        if (cover is not BitmapSource bitmap)
        {
            return null;
        }

        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return TextPageTint.FromBgra(pixels, converted.PixelWidth, converted.PixelHeight, stride);
    }

    private static PagePlacement PlacementOf(SizeD imagePixels, StoryContext context)
    {
        return HalfSheetLayout.Resolve(imagePixels, context.Sheet, false, context.MarginMm, context.Scaling, context.Numbers);
    }

    private static ImageSource Freeze(DrawingVisual visual)
    {
        var drawing = visual.Drawing;
        var image = new DrawingImage(drawing);
        if (drawing.CanFreeze)
        {
            drawing.Freeze();
        }

        if (image.CanFreeze)
        {
            image.Freeze();
        }

        return image;
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static double ToDip(double millimetres) => millimetres * Paper.DipsPerMillimetre;
}
