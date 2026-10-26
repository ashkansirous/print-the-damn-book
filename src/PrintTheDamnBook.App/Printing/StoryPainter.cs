using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.App.Printing;

/// <summary>The panel and the words over a picture, in sheet DIPs. <see cref="Words"/> is the outline, clipped to <see cref="WordsClip"/>.</summary>
internal sealed record BandPainting(Rect Panel, double CornerRadius, Geometry Words, Rect WordsClip, TextFitResult Fit);

/// <summary>The words of a text page, in sheet DIPs, clipped to <see cref="WordsClip"/>.</summary>
internal sealed record TextPagePainting(Geometry Words, Rect WordsClip, TextFitResult Fit);

/// <summary>
/// Measures and draws story text into the rectangles Core gives it (Slice 14). The words are outlines, like the page numbers,
/// so the print and the thumbnails draw exactly the same shapes. Which size, where and how big the panel is stays in Core.
/// </summary>
internal static class StoryPainter
{
    private const string FontFamilyUri = "pack://application:,,,/Fonts/#Andika";
    private const double PixelsPerDip = 1.0;
    private const double FitSlackMm = 0.01;
    private static readonly Typeface AndikaTypeface = new(new FontFamily(FontFamilyUri), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Brush InkBrush = FrozenBrush(Color.FromRgb(StoryTextLayout.Ink.R, StoryTextLayout.Ink.G, StoryTextLayout.Ink.B));
    private static readonly Brush PanelBrush = FrozenBrush(Color.FromArgb((byte)Math.Round(StoryTextLayout.PanelOpacity * 255), StoryTextLayout.PanelFill.R, StoryTextLayout.PanelFill.G, StoryTextLayout.PanelFill.B));

    public static Brush Ink => InkBrush;

    public static Brush Panel => PanelBrush;

    /// <summary>The size the words get over a picture whose visible part is <paramref name="visibleMm"/>.</summary>
    public static TextFitResult FitBand(string text, PaperKind paper, RectD visibleMm)
    {
        var area = StoryTextLayout.Band(visibleMm).Text;
        return StoryTextLayout.Choose(StoryTextLayout.BandStepsPt(paper), pt => HeightMm(Format(text, pt, area.Width, StoryTextLayout.BandLineHeight, TextAlignment.Center)) <= area.Height + FitSlackMm);
    }

    /// <summary>The size the words get on a text page.</summary>
    public static TextFitResult FitPage(string text, PaperKind paper, RectD box)
    {
        return StoryTextLayout.Choose(StoryTextLayout.PageStepsPt(paper), pt => HeightMm(Format(text, pt, box.Width, StoryTextLayout.PageLineHeight, TextAlignment.Left)) <= box.Height + FitSlackMm);
    }

    public static BandPainting PaintBand(string text, PaperKind paper, RectD visibleMm)
    {
        var area = StoryTextLayout.Band(visibleMm);
        var fit = FitBand(text, paper, visibleMm);
        var formatted = Format(text, fit.Pt, area.Text.Width, StoryTextLayout.BandLineHeight, TextAlignment.Center);
        var textHeight = HeightMm(formatted);
        var panel = StoryTextLayout.Panel(area, textHeight);
        var origin = new Point(ToDip(area.Text.X), ToDip(panel.Y + area.PaddingY));
        var clip = new Rect(ToDip(area.Text.X), ToDip(panel.Y + area.PaddingY), ToDip(area.Text.Width), ToDip(Math.Min(textHeight, area.Text.Height)));
        return new BandPainting(ToRect(panel), ToDip(area.CornerRadius), Outline(formatted, origin), clip, fit);
    }

    public static TextPagePainting PaintTextPage(string text, PaperKind paper, TextPageArea area)
    {
        var fit = FitPage(text, paper, area.Text);
        var formatted = Format(text, fit.Pt, area.Text.Width, StoryTextLayout.PageLineHeight, TextAlignment.Left);
        var textHeight = HeightMm(formatted);
        var top = StoryTextLayout.CentredTop(area.Text, textHeight);
        var origin = new Point(ToDip(area.Text.X), ToDip(top));
        var clip = new Rect(ToDip(area.Text.X), ToDip(area.Text.Y), ToDip(area.Text.Width), ToDip(area.Text.Height));
        return new TextPagePainting(Outline(formatted, origin), clip, fit);
    }

    /// <summary>Draws a picture page's band over its already drawn picture: the soft panel, then the words.</summary>
    public static void DrawBand(DrawingContext context, BandPainting band)
    {
        context.DrawRoundedRectangle(PanelBrush, null, band.Panel, band.CornerRadius, band.CornerRadius);
        context.PushClip(new RectangleGeometry(band.WordsClip));
        context.DrawGeometry(InkBrush, null, band.Words);
        context.Pop();
    }

    public static void DrawTextPage(DrawingContext context, TextPagePainting page)
    {
        context.PushClip(new RectangleGeometry(page.WordsClip));
        context.DrawGeometry(InkBrush, null, page.Words);
        context.Pop();
    }

    private static FormattedText Format(string text, double pt, double widthMm, double lineHeight, TextAlignment alignment)
    {
        var em = ToDip(StoryTextLayout.PtToMm(pt));
        return new FormattedText(text.Trim(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, AndikaTypeface, em, InkBrush, PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, ToDip(widthMm)),
            LineHeight = em * lineHeight,
            TextAlignment = alignment,
        };
    }

    private static Geometry Outline(FormattedText text, Point origin)
    {
        var geometry = text.BuildGeometry(origin);
        geometry.Freeze();
        return geometry;
    }

    private static double HeightMm(FormattedText text) => text.Height / Paper.DipsPerMillimetre;

    private static Rect ToRect(RectD mm) => new(ToDip(mm.X), ToDip(mm.Y), ToDip(mm.Width), ToDip(mm.Height));

    private static double ToDip(double millimetres) => millimetres * Paper.DipsPerMillimetre;

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
