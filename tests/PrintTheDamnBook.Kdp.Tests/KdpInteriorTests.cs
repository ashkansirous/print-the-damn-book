using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Kdp;
using UglyToad.PdfPig;

namespace PrintTheDamnBook.Kdp.Tests;

public class KdpInteriorTests
{
    private static readonly KdpInteriorResult Result = KdpInterior.Build(SyntheticBook.Input(pages: 3));

    [Fact]
    public void The_pdf_has_the_planned_number_of_pages_and_every_page_is_the_kdp_size()
    {
        using var pdf = PdfDocument.Open(Result.Pdf);

        Assert.Equal(KdpPagePlan.MinPages, pdf.NumberOfPages);
        foreach (var page in pdf.GetPages())
        {
            Assert.Equal(KdpTrim.PageWidthInches * 72, page.Width, 0.01);
            Assert.Equal(KdpTrim.PageHeightInches * 72, page.Height, 0.01);
        }
    }

    [Fact]
    public void Every_picture_page_holds_one_image_of_exactly_the_kdp_pixel_size_and_other_pages_hold_none()
    {
        using var pdf = PdfDocument.Open(Result.Pdf);

        foreach (var planned in Result.Plan.Pages)
        {
            var images = pdf.GetPage(planned.Position).GetImages().ToList();
            if (planned.Role == KdpPageRole.StoryPicture)
            {
                var image = Assert.Single(images);
                Assert.Equal(KdpTrim.PictureWidthPx, image.WidthInSamples);
                Assert.Equal(KdpTrim.PictureHeightPx, image.HeightInSamples);
            }
            else if (planned.Role == KdpPageRole.About)
            {
                var qr = Assert.Single(images); // the QR code to the Dino Reader page (Slice 37)
                Assert.Equal(KdpCoverWording.DinoReaderUrl, DecodeQr(qr));
                var text = string.Join(' ', pdf.GetPage(planned.Position).GetWords().Select(w => w.Text));
                Assert.Equal(KdpCoverWording.DinoLine, text);
            }
            else
            {
                Assert.Empty(images);
            }
        }
    }

    private static string? DecodeQr(UglyToad.PdfPig.Content.IPdfImage image)
    {
        Assert.True(image.TryGetPng(out var png));
        using var bitmap = SkiaSharp.SKBitmap.Decode(png);
        using var rgba = bitmap.Copy(SkiaSharp.SKColorType.Rgba8888);
        var source = new ZXing.RGBLuminanceSource(rgba.Bytes, rgba.Width, rgba.Height, ZXing.RGBLuminanceSource.BitmapFormat.RGBA32);
        var reader = new ZXing.BarcodeReaderGeneric { Options = new ZXing.Common.DecodingOptions { TryHarder = true, PossibleFormats = [ZXing.BarcodeFormat.QR_CODE] } };
        return reader.Decode(source)?.Text;
    }

    [Fact]
    public void Every_font_in_the_pdf_is_embedded_as_a_subset()
    {
        Assert.True(Result.Fonts.AllEmbedded);
        using var pdf = PdfDocument.Open(Result.Pdf);
        var names = pdf.GetPages().SelectMany(p => p.Letters).Select(l => l.FontName).Distinct().ToList();

        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.Matches("^[A-Z]{6}\\+", n!));
    }

    [Fact]
    public void Words_and_their_number_are_on_the_left_page_and_the_picture_faces_them()
    {
        using var pdf = PdfDocument.Open(Result.Pdf);

        var text = Result.Plan.Pages.First(p => p.Role == KdpPageRole.StoryText);
        var page = pdf.GetPage(text.Position);
        Assert.Contains("Pip was in a hurry", page.Text);
        Assert.EndsWith(text.Number!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), page.Text.Trim());
        Assert.Empty(pdf.GetPage(text.Position + 1).Letters);
    }

    [Fact]
    public void Pages_without_story_words_carry_no_page_number()
    {
        using var pdf = PdfDocument.Open(Result.Pdf);

        var blank = Result.Plan.Pages.First(p => p.Role == KdpPageRole.Blank);
        Assert.Empty(pdf.GetPage(blank.Position).Letters);
        Assert.Contains("This book belongs to", pdf.GetPage(3).Text);
    }

    [Fact]
    public void Words_stay_inside_the_safe_margin_of_their_page()
    {
        using var pdf = PdfDocument.Open(Result.Pdf);

        foreach (var planned in Result.Plan.Pages)
        {
            var trim = KdpTrim.TrimRect(planned.IsRightHand);
            var margin = KdpTrim.SafeMarginInches * 72;
            var pageHeight = KdpTrim.PageHeightInches * 72;
            foreach (var letter in pdf.GetPage(planned.Position).Letters)
            {
                var box = letter.BoundingBox;
                Assert.True(box.Left >= trim.X * 72 + margin - 0.5, $"page {planned.Position} left");
                Assert.True(box.Right <= (trim.X + trim.Width) * 72 - margin + 0.5, $"page {planned.Position} right");
                Assert.True(pageHeight - box.Top >= trim.Y * 72 + margin - 0.5, $"page {planned.Position} top");
                Assert.True(pageHeight - box.Bottom <= (trim.Y + trim.Height) * 72 - margin + 0.5, $"page {planned.Position} bottom");
            }
        }
    }

    [Fact]
    public void Long_words_wrap_at_24_pt_and_are_not_reported()
    {
        var longText = string.Join(' ', Enumerable.Repeat("Pip and Pop played in the garden.", 6));

        var result = KdpInterior.Build(SyntheticBook.Input(pages: 1, longText: longText));

        Assert.All(result.Stories, s => Assert.Equal(24, s.Pt));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("do not fit", StringComparison.Ordinal));
    }

    [Fact]
    public void Words_that_do_not_fit_the_box_at_24_pt_are_an_error_naming_the_page_and_are_not_shrunk()
    {
        var tooLong = string.Join(' ', Enumerable.Repeat("Pip and Pop played in the garden all day long.", 80));

        var result = KdpInterior.Build(SyntheticBook.Input(pages: 1, longText: tooLong));

        var story = Assert.Single(result.Stories);
        Assert.Equal(24, story.Pt);
        Assert.Contains(result.Errors, e => e.StartsWith($"Page {story.Position}: the words do not fit their box at 24 pt", StringComparison.Ordinal));
    }

    [Fact]
    public void A_right_to_left_book_mirrors_the_spread_and_embeds_the_arabic_script_font()
    {
        var result = KdpInterior.Build(SyntheticBook.Input(pages: 2, language: "fa", longText: "سلام دنیا"));

        Assert.All(result.Plan.Pages.Where(p => p.Role == KdpPageRole.StoryText), p => Assert.True(p.IsRightHand));
        Assert.True(result.Fonts.AllEmbedded);
        Assert.Contains(result.Fonts.Names, n => n.Contains("Vazirmatn", StringComparison.Ordinal));
    }

    [Fact]
    public void A_small_picture_is_reported_as_enlarged()
    {
        Assert.Contains(Result.Warnings, w => w.StartsWith("3 of 3 pictures are enlarged", StringComparison.Ordinal) && w.Contains("dpi", StringComparison.Ordinal));
    }

    [Fact]
    public void A_picture_that_is_not_an_image_is_an_error()
    {
        var input = SyntheticBook.Input(pages: 1) with { Pictures = [[1, 2, 3]] };

        Assert.Throws<KdpException>(() => KdpInterior.Build(input));
    }

    [Fact]
    public void The_check_report_lists_pages_pictures_fonts_and_warnings()
    {
        var report = KdpCheckReport.Build("Freddy and the Test", "en", Result);

        Assert.Contains("Pages: 24", report, StringComparison.Ordinal);
        Assert.Contains("8.625 x 8.75 in", report, StringComparison.Ordinal);
        Assert.Contains("Fonts embedded:", report, StringComparison.Ordinal);
        Assert.Contains("yes, every one", report, StringComparison.Ordinal);
        Assert.Contains("1792 x 2400", report, StringComparison.Ordinal);
        Assert.Contains("2588 x 2625", report, StringComparison.Ordinal);
    }
}
