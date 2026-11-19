using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PrintTheDamnBook.Kdp.Cover;

/// <summary>Everything <see cref="KdpCoverDocument"/> draws.</summary>
internal sealed record KdpCoverDrawing(string Title, string Language, DinoCredits? Credits, KdpPicture Front, KdpPicture? Back, int PageCount, ReadingDirection Direction);

/// <summary>The only drawing code for the KDP cover: one page of the whole wrap, the pictures full bleed, no spine text, the barcode box left clear (white, as KDP asks).</summary>
internal static class KdpCoverDocument
{
    private static readonly double[] TitleStepsPt = [40, 36, 32, 28, 24, 20];
    private const double TitleBoxHeightInches = 1.6;
    private const double CreditPt = 14;
    private const double AppPt = 13;
    private const double PanelInsetInches = 0.5;
    private const double BarcodeClearInches = 0.25;
    private const float LineHeight = 1.3f;
    private const string SpineColor = "#F1EEF8";

    private static readonly DocumentSettings Settings = new() { ImageCompressionQuality = ImageCompressionQuality.Best, CompressDocument = false };

    public static byte[] Render(KdpCoverDrawing drawing)
    {
        return Document.Create(container => container.Page(p => Compose(p, drawing))).WithSettings(Settings).GeneratePdf();
    }

    private static void Compose(PageDescriptor p, KdpCoverDrawing drawing)
    {
        p.Size((float)KdpCoverSize.CoverWidth(drawing.PageCount), (float)KdpCoverSize.CoverHeight, Unit.Inch);
        p.Margin(0);
        p.PageColor(Colors.White);
        p.DefaultTextStyle(s => s.FontFamily(StoryFont.Andika).FontColor(KdpInteriorDocument.Ink).LineHeight(LineHeight));
        p.Content().Layers(layers =>
        {
            layers.PrimaryLayer().Row(row => Panels(row, drawing));
            layers.Layer().Element(c => BackText(c, drawing));
            layers.Layer().Element(c => Title(c, drawing));
            layers.Layer().Element(c => Barcode(c, drawing));
        });
    }

    /// <summary>Left to right: back, spine, front; front, spine, back for a right-to-left book.</summary>
    private static void Panels(RowDescriptor row, KdpCoverDrawing drawing)
    {
        var spine = KdpCoverSize.Spine(drawing.PageCount);
        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        var left = rtl ? drawing.Front : drawing.Back;
        var right = rtl ? drawing.Back : drawing.Front;
        Picture(row.ConstantItem((float)KdpCoverSize.Spine(drawing.PageCount).X, Unit.Inch), left);
        row.ConstantItem((float)spine.Width, Unit.Inch).Background(SpineColor);
        Picture(row.RelativeItem(), right);
    }

    /// <summary>A picture filling its panel; a white panel when there is none (no back cover read).</summary>
    private static void Picture(IContainer panel, KdpPicture? picture)
    {
        if (picture is null)
        {
            panel.Background(Colors.White);
            return;
        }

        panel.Image(picture.Jpeg).UseOriginalImage().FitUnproportionally();
    }

    /// <summary>The title in a white band across the top of the front cover, inside the safe margin.</summary>
    private static void Title(IContainer layer, KdpCoverDrawing drawing)
    {
        var front = KdpCoverSize.FrontPanel(drawing.PageCount, drawing.Direction);
        var frontTrimX = drawing.Direction == ReadingDirection.RightToLeft ? front.X + KdpCoverSize.BleedInches : front.X;
        var box = new RectD(frontTrimX + PanelInsetInches, KdpCoverSize.BleedInches + PanelInsetInches, KdpTrim.TrimInches - 2 * PanelInsetInches, TitleBoxHeightInches);
        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        var pt = ChooseTitlePt(drawing.Title, box, rtl);
        KdpInteriorDocument.InBox(layer, box).Background(Colors.White).AlignMiddle().Element(words => TitleText(words, drawing.Title, pt, rtl));
    }

    /// <summary>The title set the way the interior sets story text: the font Core picks and, for a right-to-left book, right-to-left content (<see cref="KdpTextFit.Words"/>).</summary>
    private static void TitleText(IContainer words, string title, double pt, bool rtl)
    {
        KdpTextFit.Words(words, title, StoryFont.For(title), pt, rtl);
    }

    /// <summary>The largest title size that fits its box (the way the interior picks its text size); a title that fits at none stops the cover with a plain message.</summary>
    private static double ChooseTitlePt(string title, RectD box, bool rtl)
    {
        var chosen = StoryTextLayout.Choose(TitleStepsPt, pt => TitleFits(title, pt, box, rtl));
        if (!chosen.Fits)
        {
            throw new KdpException($"The title does not fit its cover box even at {chosen.Pt} pt; shorten it.");
        }

        return chosen.Pt;
    }

    /// <summary>Sets the title in a one-page document of the box's size with <c>ShowEntire</c>, which throws when it overflows (see <see cref="KdpTextFit"/>).</summary>
    private static bool TitleFits(string title, double pt, RectD box, bool rtl)
    {
        var document = Document.Create(container => container.Page(page =>
        {
            page.Size((float)box.Width, (float)box.Height, Unit.Inch);
            page.Margin(0);
            page.Content().ShowEntire().AlignMiddle().Element(words => TitleText(words, title, pt, rtl));
        }));

        try
        {
            document.GeneratePdf(Stream.Null);
            return true;
        }
        catch (DocumentLayoutException)
        {
            return false;
        }
    }

    /// <summary>The credit lines and the line pointing to the Dino app, in a white panel on the left of the back cover; nothing but the title when no back cover was read.</summary>
    private static void BackText(IContainer layer, KdpCoverDrawing drawing)
    {
        var box = BackTextBox(drawing);
        if (drawing.Back is null)
        {
            KdpInteriorDocument.InBox(layer, box).AlignTop().Text(t => TitleLine(t, drawing.Title));
            return;
        }

        var lines = drawing.Credits is { } credits ? BackCoverWording.Lines(credits, drawing.Language) : [];
        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        KdpInteriorDocument.InBox(layer, box).AlignBottom().Background(Colors.White).Padding(6).Column(column =>
        {
            column.Spacing(4);
            foreach (var line in lines)
            {
                Aligned(column.Item(), rtl).Text(t => Line(t, line, CreditPt));
            }

            Aligned(column.Item().PaddingTop(8), rtl).Text(t => Line(t, $"{KdpFrontMatterWording.AboutHeading} {KdpFrontMatterWording.AboutLine}", AppPt));
        });
    }

    /// <summary>Right-to-left content for a right-to-left book, as the interior does.</summary>
    private static IContainer Aligned(IContainer item, bool rtl) => rtl ? item.ContentFromRightToLeft() : item;

    /// <summary>The back cover's text area: the strip of the back trim on the side of the barcode box away from the spine's far corner, 0.25 in clear of the barcode.</summary>
    private static RectD BackTextBox(KdpCoverDrawing drawing)
    {
        var trim = KdpCoverSize.BackTrim(drawing.PageCount, drawing.Direction);
        var barcode = KdpCoverSize.Barcode(drawing.PageCount, drawing.Direction);
        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        var x = rtl ? barcode.X + barcode.Width + BarcodeClearInches : trim.X + PanelInsetInches;
        var right = rtl ? trim.X + trim.Width - PanelInsetInches : barcode.X - BarcodeClearInches;
        return new RectD(x, trim.Y + PanelInsetInches, right - x, trim.Height - 2 * PanelInsetInches);
    }

    private static void TitleLine(TextDescriptor t, string title)
    {
        t.DefaultTextStyle(s => s.FontFamily(StoryFont.For(title)).FontSize((float)CreditPt + 8));
        t.Span(title);
    }

    private static void Line(TextDescriptor t, string text, double pt)
    {
        t.DefaultTextStyle(s => s.FontFamily(StoryFont.For(text)).FontSize((float)pt));
        t.Span(text);
    }

    /// <summary>KDP puts its barcode here and rejects a cover with art or words in the spot, so it is a plain white box.</summary>
    private static void Barcode(IContainer layer, KdpCoverDrawing drawing)
    {
        KdpInteriorDocument.InBox(layer, KdpCoverSize.Barcode(drawing.PageCount, drawing.Direction)).Background(Colors.White);
    }
}
