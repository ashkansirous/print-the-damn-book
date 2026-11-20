using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PrintTheDamnBook.Kdp;

/// <summary>What the drawing needs for one story page: its words, the size Core chose and the soft tint of the picture it faces (null: plain white).</summary>
internal sealed record KdpStoryPage(string Text, double Pt, Rgb? Tint = null);

/// <summary>Everything <see cref="KdpInteriorDocument"/> draws: the plan, the wording, the prepared pictures and the chosen story pages (both indexed by story index).</summary>
internal sealed record KdpDrawing(
    KdpPlan Plan,
    KdpFrontMatter Matter,
    ReadingDirection Direction,
    IReadOnlyList<KdpStoryPage> Stories,
    IReadOnlyList<KdpPicture> Pictures,
    PageNumberDigits Digits,
    IReadOnlyList<byte[]>? OtherCovers = null);

/// <summary>The only drawing code for the KDP interior (Slice 27): QuestPDF pages of the KDP page size, zero margin, the picture full bleed, words and numbers inside the safe margin.</summary>
internal static class KdpInteriorDocument
{
    public const string Ink = "#2B2540";

    private const float LineHeight = 1.35f;
    private const double AboutQrInches = 1.1;
    private const double AboutGapInches = 0.3;

    /// <summary>
    /// Uncompressed so <see cref="KdpPdfInspector"/> can read the font descriptors. The raster dpi stays at QuestPDF's default (288): the page size is
    /// snapped to whole raster pixels, and at 300 dpi the 621 pt width becomes 2588 px = 621.12 pt, while 288 dpi gives exactly 621 x 630 pt. Pictures
    /// go in as their own JPEG (<c>UseOriginalImage</c>), so the raster dpi never resamples them.
    /// </summary>
    private static readonly DocumentSettings Settings = new() { ImageCompressionQuality = ImageCompressionQuality.Best, CompressDocument = false };

    /// <summary>One small PNG per page (Slice 30, <c>--previews</c>): the same drawing as the PDF, rasterised at <see cref="KdpContactSheetLayout.PageRasterDpi"/>.</summary>
    public static IReadOnlyList<byte[]> RenderPagePreviews(KdpDrawing drawing)
    {
        var settings = new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = KdpContactSheetLayout.PageRasterDpi };
        return CreateDocument(drawing).GenerateImages(settings).ToList();
    }

    internal static IDocument CreateDocument(KdpDrawing drawing)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        KdpFonts.EnsureRegistered();
        return Document.Create(container =>
        {
            foreach (var page in drawing.Plan.Pages)
            {
                container.Page(p => Compose(p, page, drawing));
            }
        }).WithSettings(Settings);
    }

    private static void Compose(PageDescriptor p, KdpPage page, KdpDrawing drawing)
    {
        p.Size((float)KdpTrim.PageWidthInches, (float)KdpTrim.PageHeightInches, Unit.Inch);
        p.Margin(0);
        p.PageColor(PaperOf(page, drawing));
        p.DefaultTextStyle(s => s.FontFamily(StoryFont.Andika).FontColor(Ink).LineHeight(LineHeight));
        p.Content().Layers(layers =>
        {
            layers.PrimaryLayer().Element(body => Body(body, page, drawing));
            if (page.Number is { } number)
            {
                layers.Layer().Element(strip => Number(strip, page, PageNumbering.Format(number, drawing.Digits), drawing.Digits));
            }
        });
    }

    /// <summary>White paper, except a story text page, which takes the soft tint of the picture it faces (Slice 36).</summary>
    private static string PaperOf(KdpPage page, KdpDrawing drawing)
    {
        if (page.Role == KdpPageRole.StoryText && drawing.Stories[page.StoryIndex!.Value].Tint is { } tint)
        {
            return $"#{tint.R:X2}{tint.G:X2}{tint.B:X2}";
        }

        return Colors.White;
    }

    private static void Body(IContainer body, KdpPage page, KdpDrawing drawing)
    {
        var matter = drawing.Matter;
        switch (page.Role)
        {
            case KdpPageRole.StoryPicture:
                body.Image(drawing.Pictures[page.StoryIndex!.Value].Jpeg).UseOriginalImage().FitUnproportionally();
                break;
            case KdpPageRole.StoryText:
                StoryText(body, page, drawing);
                break;
            case KdpPageRole.Title:
                Lines(body, page, [new Line(matter.Title, KdpTrim.TitlePt)]);
                break;
            case KdpPageRole.Copyright:
                Lines(body, page, matter.CopyrightLines.Select(l => new Line(l, KdpTrim.SmallPt)).ToList(), verticalEnd: true);
                break;
            case KdpPageRole.Dedication:
                Dedication(body, page, matter.DedicationHeading);
                break;
            case KdpPageRole.TheEnd:
                Lines(body, page, [new Line(matter.TheEnd, KdpTrim.TitlePt)]);
                break;
            case KdpPageRole.About:
                About(body, page, matter, drawing.OtherCovers ?? []);
                break;
        }
    }

    /// <summary>
    /// The last page (Slice 37, 41): the QR code to the Dino Reader page with the Dino line beside it. With other books' covers (Slice 41) the heading and the grid of covers
    /// come first and the pair last, all centred in the safe area; without, the pair alone is centred in the text box as before.
    /// </summary>
    private static void About(IContainer body, KdpPage page, KdpFrontMatter matter, IReadOnlyList<byte[]> otherCovers)
    {
        if (otherCovers.Count == 0)
        {
            DinoPair(InBox(body, KdpTrim.TextBox(page.IsRightHand)).AlignMiddle(), matter.AboutLine);
            return;
        }

        InBox(body, SafeArea(page.IsRightHand)).AlignMiddle().Column(column =>
        {
            column.Spacing((float)KdpOtherBooks.BlockGapPt);
            column.Item().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontFamily(StoryFont.For(matter.OtherBooksHeading)).FontSize((float)KdpOtherBooks.HeadingPt));
                t.Span(matter.OtherBooksHeading);
            });
            column.Item().Element(grid => CoverGrid(grid, otherCovers));
            DinoPair(column.Item(), matter.AboutLine);
        });
    }

    /// <summary>The page inside the safe margin on every side.</summary>
    private static RectD SafeArea(bool rightHandPage)
    {
        var trim = KdpTrim.TrimRect(rightHandPage);
        return new RectD(trim.X + KdpTrim.SafeMarginInches, trim.Y + KdpTrim.SafeMarginInches, trim.Width - 2 * KdpTrim.SafeMarginInches, trim.Height - 2 * KdpTrim.SafeMarginInches);
    }

    private static void DinoPair(IContainer container, string line)
    {
        var qr = Cover.KdpQrCode.Png(KdpCoverWording.DinoReaderUrl);
        container.Row(row =>
        {
            row.ConstantItem((float)AboutQrInches, Unit.Inch).Image(qr).UseOriginalImage().FitWidth();
            row.ConstantItem((float)AboutGapInches, Unit.Inch);
            row.RelativeItem().AlignMiddle().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontFamily(StoryFont.For(line)).FontSize((float)KdpTrim.MatterPt));
                t.Span(line);
            });
        });
    }

    /// <summary>The covers in rows of <see cref="KdpOtherBooks.Columns"/>, square tiles with a thin light-grey border, each row centred.</summary>
    private static void CoverGrid(IContainer grid, IReadOnlyList<byte[]> covers)
    {
        grid.Column(column =>
        {
            column.Spacing((float)KdpOtherBooks.TileGapPt);
            var next = 0;
            foreach (var size in KdpOtherBooks.RowSizes(covers.Count))
            {
                var row = covers.Skip(next).Take(size).ToList();
                next += size;
                column.Item().AlignCenter().Row(r => CoverRow(r, row));
            }
        });
    }

    private static void CoverRow(RowDescriptor row, IReadOnlyList<byte[]> covers)
    {
        row.Spacing((float)KdpOtherBooks.TileGapPt);
        foreach (var cover in covers)
        {
            row.ConstantItem((float)KdpOtherBooks.TilePt)
                .Height((float)KdpOtherBooks.TilePt)
                .Border((float)KdpOtherBooks.TileBorderPt)
                .BorderColor(KdpOtherBooks.TileBorderColor)
                .Image(cover).UseOriginalImage().FitUnproportionally();
        }
    }

    private static void StoryText(IContainer body, KdpPage page, KdpDrawing drawing)
    {
        var story = drawing.Stories[page.StoryIndex!.Value];
        var family = StoryFont.For(story.Text);
        InBox(body, KdpTrim.TextBox(page.IsRightHand)).AlignMiddle().StopPaging().Element(words => KdpTextFit.Words(words, story.Text, family, story.Pt, drawing.Direction == ReadingDirection.RightToLeft));
    }

    private sealed record Line(string Text, double Pt);

    private static void Lines(IContainer body, KdpPage page, IReadOnlyList<Line> lines, bool verticalEnd = false)
    {
        var box = InBox(body, KdpTrim.TextBox(page.IsRightHand));
        (verticalEnd ? box.AlignBottom() : box.AlignMiddle()).Column(column =>
        {
            column.Spacing(8);
            foreach (var line in lines)
            {
                column.Item().Text(t =>
                {
                    t.AlignCenter();
                    t.DefaultTextStyle(s => s.FontFamily(StoryFont.For(line.Text)).FontSize((float)line.Pt));
                    t.Span(line.Text);
                });
            }
        });
    }

    private static void Dedication(IContainer body, KdpPage page, string heading)
    {
        InBox(body, KdpTrim.TextBox(page.IsRightHand)).AlignMiddle().Column(column =>
        {
            column.Spacing(28);
            column.Item().Text(t =>
            {
                t.AlignCenter();
                t.DefaultTextStyle(s => s.FontSize((float)KdpTrim.MatterPt));
                t.Span(heading);
            });
            column.Item().Height(1).BorderBottom(1).BorderColor(Ink);
        });
    }

    private static void Number(IContainer strip, KdpPage page, string text, PageNumberDigits digits)
    {
        var family = digits == PageNumberDigits.Latin ? StoryFont.Andika : StoryFont.Vazirmatn;
        InBox(strip, KdpTrim.NumberBox(page.IsRightHand)).AlignCenter().AlignMiddle().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontFamily(family).FontSize((float)KdpTrim.NumberPt));
            t.Span(text);
        });
    }

    /// <summary>The box (inches on the page) as a container of exactly that size, placed from the page's top-left corner.</summary>
    internal static IContainer InBox(IContainer container, RectD box)
    {
        return container
            .PaddingLeft((float)KdpTrim.ToPoints(box.X))
            .PaddingTop((float)KdpTrim.ToPoints(box.Y))
            .Width((float)KdpTrim.ToPoints(box.Width))
            .Height((float)KdpTrim.ToPoints(box.Height));
    }
}
