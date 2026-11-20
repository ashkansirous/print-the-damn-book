using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp;
using SkiaSharp;
using UglyToad.PdfPig;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>Slice 36 G: each of the book's own lines stays on one line (the size shrinks until it fits), and a text page takes a soft tint from its picture.</summary>
public class KdpTextPagesTests
{
    private const string TwoLines = "Daddy made kebabs! Maybe one's there still, said Freddy\nHe searched every cupboard, each corner and nook, as he went";

    [Fact]
    public void Lines_that_wrap_at_the_first_size_stay_whole_at_a_smaller_one()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        KdpFonts.EnsureRegistered();
        var box = KdpTrim.TextBox(rightHandPage: true);

        Assert.False(KdpTextFit.LinesStayWhole(TwoLines, StoryFont.Andika, 24, box, rightToLeft: false));
        Assert.True(KdpTextFit.LinesStayWhole(TwoLines, StoryFont.Andika, 16, box, rightToLeft: false));
    }

    [Fact]
    public void A_short_page_stays_at_the_first_size()
    {
        var result = KdpInterior.Build(SyntheticBook.Input(2, longText: "Pip ran.\nPop hid."));

        Assert.All(result.Stories, s => Assert.Equal(24, s.Pt));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("wraps", StringComparison.Ordinal));
    }

    [Fact]
    public void The_size_shrinks_until_every_line_fits_on_one_line_and_the_page_has_exactly_those_lines()
    {
        var result = KdpInterior.Build(SyntheticBook.Input(2, longText: TwoLines));

        Assert.All(result.Stories, s => Assert.Equal(16, s.Pt));
        using var pdf = PdfDocument.Open(result.Pdf);
        foreach (var page in pdf.GetPages().Where(p => p.Letters.Any(l => Math.Abs(l.PointSize - 16) < 0.1)))
        {
            var lines = page.Letters.Where(l => Math.Abs(l.PointSize - 16) < 0.1).GroupBy(l => Math.Round(l.StartBaseLine.Y)).ToList();
            Assert.Equal(2, lines.Count);
        }
    }

    [Fact]
    public void A_line_too_long_for_one_line_even_at_the_smallest_size_is_reported_and_set_wrapped()
    {
        var text = string.Join(' ', Enumerable.Repeat("a long line of the story", 6));

        var result = KdpInterior.Build(SyntheticBook.Input(2, longText: text));

        Assert.Contains(result.Warnings, w => w.Contains("does not fit on one line even at 14 pt", StringComparison.Ordinal));
    }

    [Fact]
    public void One_line_of_the_font_is_a_little_taller_than_its_size()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        KdpFonts.EnsureRegistered();
        Assert.InRange(KdpTextFit.LineHeightPt(StoryFont.Andika, 100), 120, 220);
    }

    [Fact]
    public void A_font_whose_line_cannot_be_measured_never_passes_the_one_line_check()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        KdpFonts.EnsureRegistered();
        var box = new PrintTheDamnBook.Core.Layout.RectD(0, 0, 0.5, 1);

        var passed = false;
        try
        {
            passed = KdpTextFit.LinesStayWhole("a very long line that must wrap in half an inch", "No Such Font Family", 14, box, false);
        }
        catch (Exception ex) when (ex is QuestPDF.Drawing.Exceptions.DocumentDrawingException or InvalidOperationException)
        {
            passed = false;
        }

        Assert.False(passed);
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
            Assert.Equal(isTextPage, result.Pictures.Any(p => IsNear(corner, p.Tint!.Value)));
            if (isTextPage)
            {
                Assert.True(IsNear(corner, result.Pictures[page.StoryIndex!.Value].Tint!.Value));
            }
        }
    }

    private static bool IsNear(SKColor colour, Rgb tint) => Math.Abs(colour.Red - tint.R) <= 2 && Math.Abs(colour.Green - tint.G) <= 2 && Math.Abs(colour.Blue - tint.B) <= 2;
}
