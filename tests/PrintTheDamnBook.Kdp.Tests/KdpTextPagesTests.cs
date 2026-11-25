using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp;
using SkiaSharp;
using UglyToad.PdfPig;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>Slice 42: every story text page is set at 24 pt and its lines wrap; a text page takes a soft tint from its picture (Slice 36 G).</summary>
public class KdpTextPagesTests
{
    private const string TwoLines = "Daddy made kebabs! Maybe one's there still, said Freddy\nHe searched every cupboard, each corner and nook, as he went";

    [Fact]
    public void A_short_page_is_set_at_24_pt()
    {
        var result = KdpInterior.Build(SyntheticBook.Input(2, longText: "Pip ran.\nPop hid."));

        Assert.All(result.Stories, s => Assert.Equal(24, s.Pt));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("do not fit", StringComparison.Ordinal));
    }

    [Fact]
    public void Lines_too_long_for_one_line_wrap_at_24_pt_and_every_story_page_in_the_pdf_is_24_pt()
    {
        var input = SyntheticBook.Input(3) with { Texts = ["Pip ran.\nPop hid.", TwoLines, "Pip ran.\nPop hid."] };

        var result = KdpInterior.Build(input);

        Assert.All(result.Stories, s => Assert.Equal(24, s.Pt));
        using var pdf = PdfDocument.Open(result.Pdf);
        foreach (var story in result.Stories)
        {
            var letters = pdf.GetPage(story.Position).Letters;
            Assert.NotEmpty(letters);
            Assert.All(letters, l => Assert.True(Math.Abs(l.PointSize - 24) < 0.1 || Math.Abs(l.PointSize - KdpTrim.NumberPt) < 0.1, $"unexpected size {l.PointSize} on page {story.Position}"));
            Assert.Contains(letters, l => Math.Abs(l.PointSize - 24) < 0.1);
        }

        var wrapped = pdf.GetPage(result.Stories[1].Position).Letters.Where(l => Math.Abs(l.PointSize - 24) < 0.1).GroupBy(l => Math.Round(l.StartBaseLine.Y)).Count();
        Assert.True(wrapped > 2);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("do not fit", StringComparison.Ordinal));
    }

    [Fact]
    public void Right_to_left_story_pages_are_set_at_24_pt_in_Vazirmatn()
    {
        var persian = "سلام دنیا، من فردی هستم.\nامروز هوا خیلی خوب است.";

        // Two different pages: the same words on both pages would be a refrain (R7, Slice 46) and be set at 30 pt.
        var result = KdpInterior.Build(SyntheticBook.Input(2, language: "fa") with { Texts = [persian, "کتاب را باز کرد و داستان را خواند.\nبعد خوابید."] });

        Assert.All(result.Stories, s => Assert.Equal(24, s.Pt));
        Assert.All(result.Stories, s => Assert.True(s.Fits));
        using var pdf = PdfDocument.Open(result.Pdf);
        foreach (var story in result.Stories)
        {
            var letters = pdf.GetPage(story.Position).Letters.Where(l => Math.Abs(l.PointSize - 24) < 0.1).ToList();
            Assert.NotEmpty(letters);
            Assert.All(letters, l => Assert.Contains("Vazirmatn", l.FontName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void The_report_says_once_that_all_story_text_is_24_pt()
    {
        var result = KdpInterior.Build(SyntheticBook.Input(2, longText: TwoLines));

        var report = KdpCheckReport.Build("T", "en", result);

        Assert.Contains("Story text set at 24 pt on every page.", report, StringComparison.Ordinal);
        Assert.DoesNotContain("set at 16 pt", report, StringComparison.Ordinal);
    }

    private static readonly string TooLong = string.Join(' ', Enumerable.Repeat("Pip and Pop played in the garden all day long.", 80));

    [Fact]
    public void A_page_that_overflows_keeps_the_planned_page_count_and_the_pdf_stays_valid()
    {
        var result = KdpInterior.Build(SyntheticBook.Input(1, longText: TooLong));

        using var pdf = PdfDocument.Open(result.Pdf);
        Assert.Equal(result.Plan.PageCount, pdf.NumberOfPages);
        Assert.False(Assert.Single(result.Stories).Fits);
    }

    [Fact]
    public void A_page_that_overflows_is_an_error_in_the_report_and_the_summary_does_not_claim_success()
    {
        var result = KdpInterior.Build(SyntheticBook.Input(1, longText: TooLong));
        var position = Assert.Single(result.Stories).Position;

        var report = KdpCheckReport.Build("T", "en", result);

        Assert.Contains("## Errors", report, StringComparison.Ordinal);
        Assert.Contains($"Page {position}: the words do not fit their box at 24 pt", report, StringComparison.Ordinal);
        Assert.DoesNotContain("on every page.", report, StringComparison.Ordinal);
        Assert.Contains($"do not fit their box on page(s) {position}", report, StringComparison.Ordinal);
        Assert.Contains("24 pt (does not fit)", report, StringComparison.Ordinal);
    }

    [Fact]
    public void A_book_with_an_overflowing_page_still_writes_its_files_and_the_command_exits_1()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3, pageText: TooLong);
            var outFolder = Path.Combine(folder, "out");
            var error = new StringWriter();

            var code = PrintTheDamnBook.Cli.KdpRunner.Run(["kdp", dino, "--out", outFolder], new StringWriter(), error);

            Assert.Equal(PrintTheDamnBook.Cli.KdpRunner.Failed, code);
            Assert.Contains("Error: Page ", error.ToString(), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(outFolder, KdpExporter.PdfName)));
            Assert.Contains("## Errors", File.ReadAllText(Path.Combine(outFolder, KdpExporter.ReportName)), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void One_line_of_the_font_is_a_little_taller_than_its_size()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        KdpFonts.EnsureRegistered();
        Assert.InRange(KdpTextFit.LineHeightPt(StoryFont.Andika, 100), 120, 220);
    }

    [Fact]
    public void A_font_whose_line_cannot_be_measured_is_an_error()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        KdpFonts.EnsureRegistered();

        Assert.ThrowsAny<Exception>(() => KdpTextFit.LineHeightPt("No Such Font Family", 14));
    }

    [Fact]
    public void A_picture_gives_its_text_page_a_soft_tint_that_keeps_dark_words_readable()
    {
        var picture = KdpImageDecoder.Prepare(SyntheticBook.Picture(1));

        Assert.NotNull(picture.Tint);
        Assert.True(TextPageTint.LightnessOf(picture.Tint!.Value) >= TextPageTint.MinLightness);
        Assert.NotEqual(new Rgb(255, 255, 255), picture.Tint);
    }

    [Fact]
    public void The_text_pages_are_painted_with_the_tint_of_their_picture_and_the_picture_pages_are_not()
    {
        var input = SyntheticBook.Input(2);
        var result = KdpInterior.Build(input);
        var drawing = KdpInterior.DrawingFor(input, result.Plan, result.Pictures, result.Stories);

        var previews = KdpInteriorDocument.RenderPagePreviews(drawing);

        for (var i = 0; i < previews.Count; i++)
        {
            using var preview = SKBitmap.Decode(previews[i]);
            var page = result.Plan.Pages[i];
            var corner = preview.GetPixel(2, 2);
            var isTextPage = page.Role == KdpPageRole.StoryText;
            var isTinted = isTextPage || page.Role is KdpPageRole.Title or KdpPageRole.TheEnd; // the title page and The End take the tint of the first and the last text page (Slice 46)
            Assert.Equal(isTinted, result.Pictures.Any(p => IsNear(corner, p.Tint!.Value)));
            if (isTextPage)
            {
                Assert.True(IsNear(corner, result.Pictures[page.StoryIndex!.Value].Tint!.Value));
            }
        }
    }

    private static bool IsNear(SKColor colour, Rgb tint) => Math.Abs(colour.Red - tint.R) <= 2 && Math.Abs(colour.Green - tint.G) <= 2 && Math.Abs(colour.Blue - tint.B) <= 2;
}
