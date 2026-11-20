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
/// <param name="FrontText">The crop agent's box and colours for the front title (Slice 37); null uses the calm-band rule.</param>
/// <param name="BackText">Likewise for the back credit lines.</param>
/// <param name="Dino">The colours of the Dino line beside the QR code; null uses the backdrop rule.</param>
/// <param name="Notes">Where a choice that could not be used is reported (the fallback is drawn instead); null when nobody listens.</param>
internal sealed record KdpCoverDrawing(
    string Title,
    string Language,
    DinoCredits? Credits,
    KdpPicture Front,
    KdpPicture? Back,
    int PageCount,
    ReadingDirection Direction,
    KdpCoverText? FrontText = null,
    KdpCoverText? BackText = null,
    KdpTextColors? Dino = null,
    ICollection<string>? Notes = null,
    KdpTextBox? DinoBox = null);

/// <summary>
/// The only drawing code for the KDP cover: one page of the whole wrap, the pictures full bleed, no spine text, the barcode box left clear (white, as KDP asks).
/// Words sit straight on the pictures with a soft blurred shadow beneath and no box (Slice 36, 37): the title alone on the front, the credit lines on the back,
/// each in the box and colours the crop agent chose (else the calmest band, white or ink) and, at the bottom of the back on the side away from the barcode,
/// the QR code with the Dino line beside it.
/// </summary>
internal static class KdpCoverDocument
{
    private static readonly double[] TitleStepsPt = [40, 36, 32, 28, 24, 20];
    private const double TitleBoxHeightInches = 1.6;
    private const double CreditPt = 14;
    private static readonly double[] BackCreditStepsPt = [14, 13, 12, 11, 10];
    private const double BackCreditSpacingPt = 4;
    private const double AppPt = 13;
    private static readonly double[] AppStepsPt = [13, 12, 11, 10, 9];
    private const double PanelInsetInches = KdpTextBoxMaths.PanelInsetInches;
    private const double BackCreditsHeightInches = 1.5;
    private const double QrInches = KdpTextBoxMaths.QrInches;
    private const double QrGapInches = KdpTextBoxMaths.QrGapInches;
    private const double MinAppWidthInches = 0.5;
    private const float LineHeight = 1.3f;
    private const string SpineColor = "#F1EEF8";

    /// <summary>The words' colour and the colour of the soft shadow under them (null: no shadow, on the plain white panel).</summary>
    private sealed record Look(string Color, string? Shadow);

    /// <summary>The fallback look on a dark or middling picture: white letters, dark shadow.</summary>
    private static readonly Look OnDark = new("#FFFFFF", "#000000");

    /// <summary>The fallback look on a pale picture: the book's ink, white shadow.</summary>
    private static readonly Look OnLight = new(KdpInteriorDocument.Ink, "#FFFFFF");

    /// <summary>The fallback look on the plain white panel: the book's ink, no shadow.</summary>
    private static readonly Look OnPlain = new(KdpInteriorDocument.Ink, null);

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
        var (credits, dino) = BackPlacements(drawing);
        p.Content().Layers(layers =>
        {
            layers.PrimaryLayer().Row(row => Panels(row, drawing));
            FrontWords(layers, drawing);
            BackCredits(layers, drawing, credits);
            BackQr(layers, drawing, dino);
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
    /// Draws <paramref name="draw"/> in <paramref name="box"/>: with a shadow colour, first the soft shadow (one blurred image beneath, <see cref="KdpSoftShadow"/>), then the
    /// words once as real text in the look's colour; with none (the plain white panel) only the words.
    /// </summary>
    private static void Lettered(LayersDescriptor layers, KdpCoverDrawing drawing, RectD box, Look look, double pt, Action<IContainer, string> draw)
    {
        if (look.Shadow is { } shadow)
        {
            AddShadow(layers, drawing, box, pt, draw, shadow);
        }

        layers.Layer().Element(c => draw(KdpInteriorDocument.InBox(c, box), look.Color));
    }

    /// <summary>The soft shadow layer; when the shadow could not be made, no shadow and a warning (the words are still drawn).</summary>
    private static void AddShadow(LayersDescriptor layers, KdpCoverDrawing drawing, RectD box, double pt, Action<IContainer, string> draw, string shadow)
    {
        var png = KdpSoftShadow.Png(box.Width, box.Height, pt, draw, shadow);
        if (png is null)
        {
            Note(drawing, "cover: the soft shadow under some cover words could not be made (the words do not fit their box); those words have no shadow");
            return;
        }

        var padded = new RectD(box.X - KdpSoftShadow.PadInches, box.Y - KdpSoftShadow.PadInches, box.Width + 2 * KdpSoftShadow.PadInches, box.Height + 2 * KdpSoftShadow.PadInches);
        layers.Layer().Element(c => KdpInteriorDocument.InBox(c, padded).Image(png).UseOriginalImage().FitUnproportionally());
    }

    /// <summary>The fallback look for a box over <paramref name="picture"/>, whose grid covers <paramref name="panel"/> of the cover: white with a dark shadow on a dark or middling band, ink with a white shadow on a pale one.</summary>
    private static Look LookOver(KdpPicture picture, RectD panel, RectD box)
    {
        return picture.Overview is { } luma && KdpCalmBand.IsLight(KdpCalmBand.Brightness(luma, box, (panel.X, panel.Y), (panel.Width, panel.Height))) ? OnLight : OnDark;
    }

    private static Look LookOf(KdpTextColors colors) => new(colors.Color, colors.Shadow);

    /// <summary>Adds a note once: a cover drawn twice (the front tile calls <c>Create</c> again) must not repeat a warning.</summary>
    private static void Note(KdpCoverDrawing drawing, string message)
    {
        if (drawing.Notes is { } notes && !notes.Contains(message))
        {
            notes.Add(message);
        }
    }

    /// <summary>The title alone on the front (Slice 37), in the box and colours the crop agent chose; with none (or none usable) in the calmest band of the front picture.</summary>
    private static void FrontWords(LayersDescriptor layers, KdpCoverDrawing drawing)
    {
        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        var (box, pt, look) = ChosenFront(drawing, rtl) ?? FallbackFront(drawing, rtl);
        Lettered(layers, drawing, box, look, pt, (c, color) => c.AlignMiddle().Element(words => KdpTextFit.Words(words, drawing.Title, StoryFont.For(drawing.Title), pt, rtl, color)));
    }

    private static (RectD Box, double Pt, Look Look)? ChosenFront(KdpCoverDrawing drawing, bool rtl)
    {
        if (drawing.FrontText is not { } choice)
        {
            return null;
        }

        var trim = KdpCoverSize.FrontTrim(drawing.PageCount, drawing.Direction);
        if (KdpTextBoxMaths.Resolve(choice.Box, trim, null, null, KdpCoverSize.FrontPanel(drawing.PageCount, drawing.Direction)) is not { } box)
        {
            Note(drawing, UnusableBox(KdpPictureKeys.Front));
            return null;
        }

        if (TryChooseTitlePt(drawing.Title, box, rtl) is not { } pt)
        {
            Note(drawing, $"cover: the title does not fit the text box saved for the front cover (even at {TitleStepsPt[^1]} pt); the calm-band placement is used instead");
            return null;
        }

        return (box, pt, new Look(choice.Color, choice.Shadow));
    }

    private static string UnusableBox(string key) =>
        $"cover: the text box saved for the {key} cover is too small (or loses more than half its height) once it is kept inside the safe margin and clear of the barcode and QR code; the fallback placement is used instead";

    /// <summary>Slice 36's rule: the calmest band of the front picture, white letters with a dark shadow (ink with a white one on a pale band).</summary>
    private static (RectD Box, double Pt, Look Look) FallbackFront(KdpCoverDrawing drawing, bool rtl)
    {
        var width = KdpTrim.TrimInches - 2 * PanelInsetInches;
        var pt = ChooseTitlePt(drawing.Title, new RectD(0, 0, width, TitleBoxHeightInches), rtl);
        var titleHeight = TextHeight(drawing.Title, pt, width, TitleBoxHeightInches, rtl);
        var band = FrontBand(drawing, titleHeight);
        var box = new RectD(band.X, band.Y, band.Width, titleHeight);
        return (box, pt, LookOver(drawing.Front, KdpCoverSize.FrontPanel(drawing.PageCount, drawing.Direction), box));
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
        var trim = KdpCoverSize.FrontTrim(drawing.PageCount, drawing.Direction);
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

    /// <summary>The largest title size that fits the box, or null when it fits at none.</summary>
    private static double? TryChooseTitlePt(string title, RectD box, bool rtl)
    {
        var chosen = StoryTextLayout.Choose(TitleStepsPt, pt => TitleFits(title, pt, box, rtl));
        return chosen.Fits ? chosen.Pt : null;
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

    /// <summary>The credit lines on the back cover, in the box and colours the crop agent chose (else at the top); with no back picture, only the title on the white panel.</summary>
    private static void BackCredits(LayersDescriptor layers, KdpCoverDrawing drawing, CreditsPlacement? placed)
    {
        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        if (drawing.Back is null)
        {
            var box = DefaultCreditsBox(KdpCoverSize.BackTrim(drawing.PageCount, drawing.Direction));
            Lettered(layers, drawing, box, OnPlain, CreditPt + 8, (c, color) => c.AlignTop().Text(t => Line(t, drawing.Title, CreditPt + 8, color)));
            return;
        }

        if (placed is null)
        {
            return;
        }

        Lettered(layers, drawing, placed.Box, placed.Look, placed.Pt, (c, color) => c.AlignTop().Column(column =>
        {
            column.Spacing((float)BackCreditSpacingPt);
            foreach (var line in placed.Lines)
            {
                Aligned(column.Item(), rtl).Text(t => Line(t, line, placed.Pt, color));
            }
        }));
    }

    /// <summary>Where the credit lines go and how they look.</summary>
    private sealed record CreditsPlacement(RectD Box, double Pt, Look Look, IReadOnlyList<string> Lines);

    /// <summary>Where the QR code and the Dino line go: the whole block, the code, the line's box and size, and the look of the line.</summary>
    private sealed record DinoPlacement(RectD Block, RectD Qr, RectD Words, double Pt, Look Look);

    /// <summary>
    /// The back's credits and Dino block, decided together (Slice 41): the Dino block goes in the box the crop agent chose when that box is usable and does not meet the
    /// credits, else in the bottom corner (with a note saying why); the credits keep clear of the bottom strip only while the block is in the corner.
    /// </summary>
    private static (CreditsPlacement? Credits, DinoPlacement? Dino) BackPlacements(KdpCoverDrawing drawing)
    {
        if (drawing.Back is null)
        {
            return (null, null);
        }

        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        var trim = KdpCoverSize.BackTrim(drawing.PageCount, drawing.Direction);
        var lines = drawing.Credits is { } credits ? BackCoverWording.Lines(credits, drawing.Language) : null;
        var placed = ChosenDino(drawing, trim, rtl);
        var attempt = new List<string>();
        var creditsPlacement = lines is null ? null : CreditsFor(drawing, trim, lines, rtl, placed is null, attempt);
        if (placed is not null && !KdpTextBoxMaths.ChosenBlockClearOfCredits(placed.Block, creditsPlacement?.Box))
        {
            Note(drawing, "cover: the Dino block box saved for the back cover meets the credit lines; the QR code and line stay in the bottom corner (decide the Dino box again with --dino-text)");
            placed = null;
            attempt.Clear();
            creditsPlacement = CreditsFor(drawing, trim, lines!, rtl, true, attempt);
        }

        attempt.ForEach(n => Note(drawing, n));
        return (creditsPlacement, placed ?? CornerDino(drawing, trim, rtl));
    }

    private static CreditsPlacement CreditsFor(KdpCoverDrawing drawing, RectD trim, IReadOnlyList<string> lines, bool rtl, bool clearStrip, ICollection<string> notes)
    {
        var (box, pt, look) = ChosenBack(drawing, trim, lines, rtl, clearStrip, notes) ?? FallbackBack(drawing, trim, lines, rtl);
        return new CreditsPlacement(box, pt, look, lines);
    }

    private static RectD DefaultCreditsBox(RectD trim) => new(trim.X + PanelInsetInches, trim.Y + PanelInsetInches, trim.Width - 2 * PanelInsetInches, BackCreditsHeightInches);

    private static (RectD Box, double Pt, Look Look)? ChosenBack(KdpCoverDrawing drawing, RectD trim, IReadOnlyList<string> lines, bool rtl, bool clearStrip, ICollection<string> notes)
    {
        if (drawing.BackText is not { } choice)
        {
            return null;
        }

        var barcode = KdpCoverSize.Barcode(drawing.PageCount, drawing.Direction);
        var strip = clearStrip ? KdpTextBoxMaths.QrStrip(trim) : (RectD?)null;
        if (KdpTextBoxMaths.Resolve(choice.Box, trim, barcode, strip, KdpCoverSize.BackPanel(drawing.PageCount, drawing.Direction)) is not { } box)
        {
            notes.Add(UnusableBox(KdpPictureKeys.Back));
            return null;
        }

        if (TryChooseBackCreditPt(lines, box, rtl) is not { } pt)
        {
            notes.Add($"cover: the credit lines do not fit the text box saved for the back cover (even at {BackCreditStepsPt[^1]} pt); the fallback placement is used instead");
            return null;
        }

        return (box, pt, new Look(choice.Color, choice.Shadow));
    }

    /// <summary>Slice 36's rule: the top of the back, white letters with a dark shadow (ink with a white one on a pale band).</summary>
    private static (RectD Box, double Pt, Look Look) FallbackBack(KdpCoverDrawing drawing, RectD trim, IReadOnlyList<string> lines, bool rtl)
    {
        var box = DefaultCreditsBox(trim);
        var look = LookOver(drawing.Back!, KdpCoverSize.BackPanel(drawing.PageCount, drawing.Direction), box);
        return (box, ChooseBackCreditPt(lines, box, rtl), look);
    }

    /// <summary>The largest size at which all the credit lines fit their box (with the gaps between them); at none the cover stops with a plain message.</summary>
    private static double ChooseBackCreditPt(IReadOnlyList<string> lines, RectD box, bool rtl)
    {
        return TryChooseBackCreditPt(lines, box, rtl) ?? throw new KdpException($"The credit lines do not fit the back cover even at {BackCreditStepsPt[^1]} pt; shorten the names.");
    }

    internal static double? TryChooseBackCreditPt(IReadOnlyList<string> lines, RectD box, bool rtl)
    {
        var text = string.Join('\n', lines);
        var gaps = Math.Max(0, lines.Count - 1) * BackCreditSpacingPt / KdpTrim.PointsPerInch;
        if (box.Height - gaps <= 0)
        {
            return null;
        }

        var inner = new RectD(0, 0, box.Width, box.Height - gaps);
        var chosen = StoryTextLayout.Choose(BackCreditStepsPt, pt => KdpTextFit.Fits(text, StoryFont.For(text), pt, inner, rtl));
        return chosen.Fits ? chosen.Pt : null;
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
    /// The QR code to the Dino Reader page, about 0.8 in, at the bottom of the back cover on the side away from the barcode, with the Dino line beside it
    /// (colours as the crop agent chose, else the backdrop rule). A cover with no back picture has neither (its panel is a plain white one).
    /// </summary>
    private static void BackQr(LayersDescriptor layers, KdpCoverDrawing drawing, DinoPlacement? placed)
    {
        if (placed is null)
        {
            return;
        }

        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        var png = KdpQrCode.Png(KdpCoverWording.DinoReaderUrl);
        layers.Layer().Element(c => KdpInteriorDocument.InBox(c, placed.Qr).Image(png).UseOriginalImage().FitUnproportionally());
        Lettered(layers, drawing, placed.Words, placed.Look, placed.Pt, (c, color) => c.AlignMiddle().Element(words => Aligned(words, rtl).Text(t => Line(t, KdpCoverWording.DinoLine, placed.Pt, color))));
    }

    /// <summary>The look of the Dino line: the crop agent's colours, else the backdrop rule over the line's own box.</summary>
    private static Look DinoLook(KdpCoverDrawing drawing, RectD wordsBox)
    {
        return drawing.Dino is { } colors ? LookOf(colors) : LookOver(drawing.Back!, KdpCoverSize.BackPanel(drawing.PageCount, drawing.Direction), wordsBox);
    }

    /// <summary>The bottom-corner placement of Slice 36 and 37: the QR code on the side away from the barcode, the line beside it.</summary>
    private static DinoPlacement CornerDino(KdpCoverDrawing drawing, RectD trim, bool rtl)
    {
        var (block, qr, wordsBox) = KdpTextBoxMaths.CornerDinoBlock(trim, KdpCoverSize.Barcode(drawing.PageCount, drawing.Direction), rtl);
        return new DinoPlacement(block, qr, wordsBox, ChooseAppPt(KdpCoverWording.DinoLine, wordsBox, rtl), DinoLook(drawing, wordsBox));
    }

    /// <summary>
    /// The Dino block in the box the crop agent chose (Slice 41): the QR code at the box's edge on the reading side, vertically centred, the line filling the rest.
    /// Null, with a note, when there is no usable box (none saved, too small, outside the safe margin, meeting the barcode) or the line does not fit it.
    /// </summary>
    private static DinoPlacement? ChosenDino(KdpCoverDrawing drawing, RectD trim, bool rtl)
    {
        if (drawing.DinoBox is not { } fractions)
        {
            return null;
        }

        var barcode = KdpCoverSize.Barcode(drawing.PageCount, drawing.Direction);
        if (KdpTextBoxMaths.ResolveBlock(fractions, trim, barcode, KdpCoverSize.BackPanel(drawing.PageCount, drawing.Direction)) is not { } block)
        {
            Note(drawing, "cover: the Dino block box saved for the back cover is too small, outside the safe margin or meets the barcode; the QR code and line stay in the bottom corner");
            return null;
        }

        var (qr, words) = KdpTextBoxMaths.SplitDinoBlock(block, rtl);
        if (TryChooseAppPt(KdpCoverWording.DinoLine, words, rtl) is not { } pt)
        {
            Note(drawing, "cover: the Dino line does not fit beside the QR code in the Dino block box saved for the back cover; the QR code and line stay in the bottom corner");
            return null;
        }

        return new DinoPlacement(block, qr, words, pt, DinoLook(drawing, words));
    }

    /// <summary>The largest size at which the Dino line fits beside the QR code, or null when it fits at none (or the box is too narrow).</summary>
    internal static double? TryChooseAppPt(string text, RectD box, bool rtl)
    {
        if (box.Width < MinAppWidthInches || box.Height <= 0)
        {
            return null;
        }

        var chosen = StoryTextLayout.Choose(AppStepsPt, pt => KdpTextFit.Fits(text, StoryFont.For(text), pt, new RectD(0, 0, box.Width, box.Height), rtl));
        return chosen.Fits ? chosen.Pt : null;
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
