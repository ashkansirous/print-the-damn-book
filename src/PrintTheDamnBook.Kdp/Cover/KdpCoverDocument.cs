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

/// <summary>
/// The only drawing code for the KDP cover: one page of the whole wrap, the pictures full bleed, no spine text, the barcode box left clear (white, as KDP asks).
/// Words sit straight on the pictures, white with a soft dark shadow and no box (Slice 36): the title and the credit line in the calmest band of the front,
/// the credit lines at the top of the back and, at the bottom of the back on the side away from the barcode, the QR code with the Dino line beside it.
/// </summary>
internal static class KdpCoverDocument
{
    private static readonly double[] TitleStepsPt = [40, 36, 32, 28, 24, 20];
    private const double TitleBoxHeightInches = 1.6;
    private const double FrontCreditHeightInches = 0.7;
    private const double TitleCreditGapInches = 0.08;
    private static readonly double[] CreditStepsPt = [15, 14, 13, 12, 11];
    private const double CreditPt = 14;
    private static readonly double[] BackCreditStepsPt = [14, 13, 12, 11, 10];
    private const double BackCreditSpacingPt = 4;
    private const double AppPt = 13;
    private static readonly double[] AppStepsPt = [13, 12, 11, 10, 9];
    private const double PanelInsetInches = 0.5;
    private const double BackCreditsHeightInches = 1.5;
    private const double QrInches = 0.8;
    private const double QrGapInches = 0.2;
    private const double MinAppWidthInches = 0.5;
    private const double BarcodeClearInches = 0.25;
    private const float LineHeight = 1.3f;
    private const string SpineColor = "#F1EEF8";
    private const string LetterColor = "#FFFFFF";

    /// <summary>The same halo in white, around dark ink on a pale picture.</summary>
    private const string HaloColor = "#99FFFFFF";

    /// <summary>A dark halo a little wider than the letters: rings of eight faint copies around them add up to a soft shadow that keeps white letters readable. A light band gets wider rings and a stronger shadow.</summary>
    private static readonly double[] ShadowRadiiInches = [0.015, 0.03];

    private static readonly double[] StrongShadowRadiiInches = [0.02, 0.04, 0.06];
    private const int ShadowDirections = 8;

    private static readonly (double X, double Y)[] ShadowOffsets = Rings(ShadowRadiiInches);
    private static readonly (double X, double Y)[] StrongShadowOffsets = Rings(StrongShadowRadiiInches);

    private static (double X, double Y)[] Rings(double[] radii)
    {
        return radii
            .SelectMany(radius => Enumerable.Range(0, ShadowDirections).Select(i => (radius * Math.Cos(2 * Math.PI * i / ShadowDirections), radius * Math.Sin(2 * Math.PI * i / ShadowDirections))))
            .ToArray();
    }

    private static string ShadowColor(byte alpha) => $"#{alpha:X2}000000";

    private static readonly DocumentSettings Settings = new() { ImageCompressionQuality = ImageCompressionQuality.Best, CompressDocument = false };

    public static byte[] Render(KdpCoverDrawing drawing)
    {
        return Create(drawing).GeneratePdf();
    }

    internal static IDocument Create(KdpCoverDrawing drawing)
    {
        return Document.Create(container => container.Page(p => Compose(p, drawing))).WithSettings(Settings);
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
            FrontWords(layers, drawing);
            BackCredits(layers, drawing);
            BackQr(layers, drawing);
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

    /// <summary>
    /// Draws <paramref name="draw"/> in <paramref name="box"/>: on a picture first as faint dark copies around the spot (the shadow), then in white; on a
    /// plain white panel only once, in the book's dark ink.
    /// </summary>
    private static void Lettered(LayersDescriptor layers, RectD box, Backdrop backdrop, Action<IContainer, string> draw, double? bandBrightness = null)
    {
        if (backdrop != Backdrop.Plain)
        {
            var alpha = KdpCalmBand.ShadowAlpha(bandBrightness ?? 0);
            var strong = alpha == KdpCalmBand.StrongShadowAlpha;
            foreach (var (dx, dy) in strong ? StrongShadowOffsets : ShadowOffsets)
            {
                var shifted = new RectD(box.X + dx, box.Y + dy, box.Width, box.Height);
                layers.Layer().Element(c => draw(KdpInteriorDocument.InBox(c, shifted), backdrop == Backdrop.Dark ? ShadowColor(alpha) : HaloColor));
            }
        }

        layers.Layer().Element(c => draw(KdpInteriorDocument.InBox(c, box), backdrop == Backdrop.Dark ? LetterColor : KdpInteriorDocument.Ink));
    }

    /// <summary>What the words sit on: a dark picture (white letters, dark shadow), a pale one (the book's ink, white halo) or the plain white panel (ink, nothing around it).</summary>
    private enum Backdrop
    {
        Dark,
        Light,
        Plain,
    }

    /// <summary>The words' look for a box over <paramref name="picture"/>, whose grid covers <paramref name="panel"/> of the cover: white on a dark or middling band, ink on a pale one.</summary>
    private static Backdrop BackdropOver(KdpPicture picture, RectD panel, RectD box)
    {
        return picture.Overview is { } luma && KdpCalmBand.IsLight(KdpCalmBand.Brightness(luma, box, (panel.X, panel.Y), (panel.Width, panel.Height))) ? Backdrop.Light : Backdrop.Dark;
    }

    /// <summary>The title and, under it, "Written by ... · Illustrated by ...", in the calmest band of the front picture.</summary>
    private static void FrontWords(LayersDescriptor layers, KdpCoverDrawing drawing)
    {
        var credit = KdpCoverWording.FrontCredit(drawing.Credits, drawing.Language);
        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        var width = KdpTrim.TrimInches - 2 * PanelInsetInches;
        var pt = ChooseTitlePt(drawing.Title, new RectD(0, 0, width, TitleBoxHeightInches), rtl);
        var titleHeight = TextHeight(drawing.Title, pt, width, TitleBoxHeightInches, rtl);
        var creditPt = credit is null ? 0 : ChooseCreditPt(credit, width, rtl);
        var creditHeight = credit is null ? 0 : TextHeight(credit, creditPt, width, FrontCreditHeightInches, rtl);
        var band = FrontBand(drawing, titleHeight + creditHeight + (credit is null ? 0 : TitleCreditGapInches));
        var titleBox = new RectD(band.X, band.Y, band.Width, titleHeight);
        var panel = KdpCoverSize.FrontPanel(drawing.PageCount, drawing.Direction);
        Lettered(layers, titleBox, Backdrop.Dark, (c, color) => c.AlignTop().Element(words => KdpTextFit.Words(words, drawing.Title, StoryFont.For(drawing.Title), pt, rtl, color)), MeanBrightness(drawing.Front, panel, titleBox));
        if (credit is not null)
        {
            var creditBox = new RectD(band.X, band.Y + titleHeight + TitleCreditGapInches, band.Width, creditHeight);
            Lettered(layers, creditBox, Backdrop.Dark, (c, color) => c.AlignTop().Element(words => KdpTextFit.Words(words, credit, StoryFont.For(credit), creditPt, rtl, color)), MeanBrightness(drawing.Front, panel, creditBox));
        }
    }

    /// <summary>The mean brightness of the picture under a box; null when the picture has no brightness grid.</summary>
    private static double? MeanBrightness(KdpPicture picture, RectD panel, RectD box)
    {
        return picture.Overview is { } luma ? KdpCalmBand.Brightness(luma, box, (panel.X, panel.Y), (panel.Width, panel.Height)) : null;
    }

    /// <summary>The largest size at which the credit line stays on one line; at none, the largest at which it wraps inside its box; at none, the cover stops with a plain message.</summary>
    internal static double ChooseCreditPt(string credit, double width, bool rtl)
    {
        var box = new RectD(0, 0, width, FrontCreditHeightInches);
        var whole = StoryTextLayout.Choose(CreditStepsPt, pt => KdpTextFit.LinesStayWhole(credit, StoryFont.For(credit), pt, box, rtl));
        if (whole.Fits)
        {
            return whole.Pt;
        }

        var wrapped = StoryTextLayout.Choose(CreditStepsPt, pt => KdpTextFit.Fits(credit, StoryFont.For(credit), pt, box, rtl));
        return wrapped.Fits ? wrapped.Pt : throw new KdpException($"The front credit does not fit the front cover even at {wrapped.Pt} pt; shorten the names.");
    }

    /// <summary>The height the words really take (their wrapped lines), at most the box they were chosen to fit.</summary>
    private static double TextHeight(string text, double pt, double width, double boxHeight, bool rtl)
    {
        return KdpTextFit.TightHeightInches(text, StoryFont.For(text), pt, width, boxHeight, rtl);
    }

    /// <summary>The band of the front trim, inside the safe margin, whose picture is calmest (<see cref="KdpCalmBand"/>); the title goes there.</summary>
    private static RectD FrontBand(KdpCoverDrawing drawing, double wordsHeight)
    {
        var panel = KdpCoverSize.FrontPanel(drawing.PageCount, drawing.Direction);
        var trimX = drawing.Direction == ReadingDirection.RightToLeft ? panel.X + KdpCoverSize.BleedInches : panel.X;
        var trim = new RectD(trimX, KdpCoverSize.BleedInches, KdpTrim.TrimInches, KdpTrim.TrimInches);
        var bands = KdpCalmBand.Candidates(trim, KdpTrim.TrimInches - 2 * PanelInsetInches, wordsHeight);
        var index = drawing.Front.Overview is { } luma ? KdpCalmBand.Choose(bands, luma, (panel.X, panel.Y), (panel.Width, panel.Height)) : 0;
        return bands[index];
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

    /// <summary>The credit lines at the top of the back cover; with no back picture, only the title on the white panel.</summary>
    private static void BackCredits(LayersDescriptor layers, KdpCoverDrawing drawing)
    {
        var trim = KdpCoverSize.BackTrim(drawing.PageCount, drawing.Direction);
        var box = new RectD(trim.X + PanelInsetInches, trim.Y + PanelInsetInches, trim.Width - 2 * PanelInsetInches, BackCreditsHeightInches);
        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        if (drawing.Back is null)
        {
            Lettered(layers, box, Backdrop.Plain, (c, color) => c.AlignTop().Text(t => Line(t, drawing.Title, CreditPt + 8, color)));
            return;
        }

        if (drawing.Credits is not { } credits)
        {
            return;
        }

        var lines = BackCoverWording.Lines(credits, drawing.Language);
        var backdrop = BackdropOver(drawing.Back, KdpCoverSize.BackPanel(drawing.PageCount, drawing.Direction), box);
        var creditPt = ChooseBackCreditPt(lines, box.Width, rtl);
        Lettered(layers, box, backdrop, (c, color) => c.AlignTop().Column(column =>
        {
            column.Spacing((float)BackCreditSpacingPt);
            foreach (var line in lines)
            {
                Aligned(column.Item(), rtl).Text(t => Line(t, line, creditPt, color));
            }
        }));
    }

    /// <summary>The largest size at which all the credit lines fit their box (with the gaps between them); at none the cover stops with a plain message.</summary>
    private static double ChooseBackCreditPt(IReadOnlyList<string> lines, double width, bool rtl)
    {
        var text = string.Join('\n', lines);
        var gaps = Math.Max(0, lines.Count - 1) * BackCreditSpacingPt / KdpTrim.PointsPerInch;
        var box = new RectD(0, 0, width, BackCreditsHeightInches - gaps);
        var chosen = StoryTextLayout.Choose(BackCreditStepsPt, pt => KdpTextFit.Fits(text, StoryFont.For(text), pt, box, rtl));
        return chosen.Fits ? chosen.Pt : throw new KdpException($"The credit lines do not fit the back cover even at {chosen.Pt} pt; shorten the names.");
    }

    /// <summary>The largest size at which the Dino line fits beside the QR code; at none the cover stops with a plain message.</summary>
    internal static double ChooseAppPt(string text, RectD box, bool rtl)
    {
        if (box.Width < MinAppWidthInches || box.Height <= 0)
        {
            throw new KdpException($"There is no room for the Dino line beside the QR code (it would be {Math.Max(0, box.Width):0.##} in wide).");
        }

        var chosen = StoryTextLayout.Choose(AppStepsPt, pt => KdpTextFit.Fits(text, StoryFont.For(text), pt, new RectD(0, 0, box.Width, box.Height), rtl));
        return chosen.Fits ? chosen.Pt : throw new KdpException($"The Dino line does not fit beside the QR code even at {chosen.Pt} pt.");
    }

    /// <summary>
    /// The QR code to the Dino Reader page, about 0.8 in, at the bottom of the back cover on the side away from the barcode, with the Dino line beside it.
    /// A cover with no back picture has neither (its panel is a plain white one).
    /// </summary>
    private static void BackQr(LayersDescriptor layers, KdpCoverDrawing drawing)
    {
        if (drawing.Back is null)
        {
            return;
        }

        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        var trim = KdpCoverSize.BackTrim(drawing.PageCount, drawing.Direction);
        var barcode = KdpCoverSize.Barcode(drawing.PageCount, drawing.Direction);
        var y = trim.Y + trim.Height - PanelInsetInches - QrInches;
        var qrX = rtl ? trim.X + trim.Width - PanelInsetInches - QrInches : trim.X + PanelInsetInches;
        var wordsX = rtl ? barcode.X + barcode.Width + BarcodeClearInches : qrX + QrInches + QrGapInches;
        var wordsRight = rtl ? qrX - QrGapInches : barcode.X - BarcodeClearInches;
        var png = KdpQrCode.Png(KdpCoverWording.DinoReaderUrl);
        layers.Layer().Element(c => KdpInteriorDocument.InBox(c, new RectD(qrX, y, QrInches, QrInches)).Image(png).UseOriginalImage().FitUnproportionally());
        var wordsBox = new RectD(wordsX, y, wordsRight - wordsX, QrInches);
        var backdrop = BackdropOver(drawing.Back, KdpCoverSize.BackPanel(drawing.PageCount, drawing.Direction), wordsBox);
        var appPt = ChooseAppPt(KdpCoverWording.DinoLine, wordsBox, rtl);
        Lettered(layers, wordsBox, backdrop, (c, color) => c.AlignMiddle().Element(words => Aligned(words, rtl).Text(t => Line(t, KdpCoverWording.DinoLine, appPt, color))));
    }

    /// <summary>Right-to-left content for a right-to-left book, as the interior does.</summary>
    private static IContainer Aligned(IContainer item, bool rtl) => rtl ? item.ContentFromRightToLeft() : item;

    private static void Line(TextDescriptor t, string text, double pt, string color)
    {
        t.DefaultTextStyle(s => s.FontFamily(StoryFont.For(text)).FontSize((float)pt).FontColor(color));
        t.Span(text);
    }

    /// <summary>KDP puts its barcode here and rejects a cover with art or words in the spot, so it is a plain white box.</summary>
    private static void Barcode(IContainer layer, KdpCoverDrawing drawing)
    {
        KdpInteriorDocument.InBox(layer, KdpCoverSize.Barcode(drawing.PageCount, drawing.Direction)).Background(Colors.White);
    }
}
