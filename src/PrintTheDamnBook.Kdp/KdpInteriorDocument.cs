using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp.Styling;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PrintTheDamnBook.Kdp;

/// <summary>What the drawing needs for one story page: its words, the size Core chose and the soft tint of the picture it faces (null: plain white).</summary>
internal sealed record KdpStoryPage(StyledPage Styled, double Pt, Rgb? Tint = null);

/// <summary>Everything <see cref="KdpInteriorDocument"/> draws: the plan, the wording, the prepared pictures and the chosen story pages (both indexed by story index).</summary>
internal sealed record KdpDrawing(
    KdpPlan Plan,
    KdpFrontMatter Matter,
    ReadingDirection Direction,
    IReadOnlyList<KdpStoryPage> Stories,
    IReadOnlyList<KdpPicture> Pictures,
    PageNumberDigits Digits,
    IReadOnlyList<byte[]>? OtherCovers = null,
    KdpFrontPages? Front = null);

/// <summary>The only drawing code for the KDP interior (Slice 27): QuestPDF pages of the KDP page size, zero margin, the picture full bleed, words and numbers inside the safe margin.</summary>
internal static class KdpInteriorDocument
{
    public const string Ink = "#2B2540";

    private const float LineHeight = 1.35f;

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

    /// <summary>White paper, except a story text page, which takes the soft tint of the picture it faces (Slice 36), and the title page and The End, which take the tint of the first and of the last story text page (Slice 46).</summary>
    private static string PaperOf(KdpPage page, KdpDrawing drawing)
    {
        var tint = page.Role switch
        {
            KdpPageRole.StoryText => drawing.Stories[page.StoryIndex!.Value].Tint,
            KdpPageRole.Title => drawing.Front?.TitleTint,
            KdpPageRole.TheEnd => drawing.Front?.EndTint,
            _ => null,
        };
        return tint is { } rgb ? $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}" : Colors.White;
    }

    private static void Body(IContainer body, KdpPage page, KdpDrawing drawing)
    {
        var matter = drawing.Matter;
        var rtl = drawing.Direction == ReadingDirection.RightToLeft;
        switch (page.Role)
        {
            case KdpPageRole.StoryPicture:
                body.Image(drawing.Pictures[page.StoryIndex!.Value].Jpeg).UseOriginalImage().FitUnproportionally();
                break;
            case KdpPageRole.StoryText:
                StoryText(body, page, drawing);
                break;
            case KdpPageRole.Title:
                KdpMatterPages.Title(body, page, drawing.Front!, rtl);
                break;
            case KdpPageRole.Credits:
                KdpMatterPages.Credits(body, page, matter, rtl);
                break;
            case KdpPageRole.BelongsTo:
                KdpMatterPages.BelongsTo(body, page, matter, rtl);
                break;
            case KdpPageRole.TheEnd:
                KdpMatterPages.TheEnd(body, page, matter, drawing.Front!, rtl);
                break;
            case KdpPageRole.MoreBooks:
                MoreBooks(body, page, matter, drawing.OtherCovers ?? []);
                break;
            case KdpPageRole.Dino:
                KdpMatterPages.Dino(body, page, drawing.Front!, rtl);
                break;
        }
    }

    /// <summary>The "More books by Ashkan Sirous" page (Slice 41's heading and grid, its look unchanged, on its own page since Slice 46): centred in the safe area.</summary>
    private static void MoreBooks(IContainer body, KdpPage page, KdpFrontMatter matter, IReadOnlyList<byte[]> otherCovers)
    {
        InBox(body, SafeArea(page.IsRightHand)).AlignMiddle().Column(column =>
        {
            column.Spacing((float)KdpOtherBooks.BlockGapPt);
            column.Item().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontFamily(StoryFont.For(matter.OtherBooksHeading)).FontSize((float)KdpOtherBooks.HeadingPt));
                t.Span(matter.OtherBooksHeading);
            });
            column.Item().Element(grid => CoverGrid(grid, otherCovers));
        });
    }

    /// <summary>The page inside the safe margin on every side.</summary>
    private static RectD SafeArea(bool rightHandPage)
    {
        var trim = KdpTrim.TrimRect(rightHandPage);
        return new RectD(trim.X + KdpTrim.SafeMarginInches, trim.Y + KdpTrim.SafeMarginInches, trim.Width - 2 * KdpTrim.SafeMarginInches, trim.Height - 2 * KdpTrim.SafeMarginInches);
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
        var family = StoryFont.For(story.Styled.Text);
        InBox(body, KdpTrim.TextBox(page.IsRightHand)).AlignMiddle().StopPaging().Element(words => KdpStyledText.Words(words, story.Styled, family, drawing.Direction == ReadingDirection.RightToLeft));
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
