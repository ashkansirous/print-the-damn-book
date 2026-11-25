using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp;
using PrintTheDamnBook.Kdp.Cover;
using UglyToad.PdfPig;

namespace PrintTheDamnBook.Kdp.Tests;

public class KdpCoverTests
{
    private static readonly DinoCredits Credits = new("Ann Author", false, "Ashkan Sirous", 2026, 11);

    private static KdpCoverInput Input(int pages, bool withBack) =>
        new("Freddy and the Test", "en", withBack ? Credits : null, SyntheticBook.Picture(1), withBack ? SyntheticBook.Picture(2) : null, pages);

    [Theory]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(40)]
    public void The_cover_pdf_is_one_page_of_the_kdp_formula_size(int pages)
    {
        var result = KdpCover.Build(Input(pages, withBack: true));

        using var pdf = PdfDocument.Open(result.Pdf);
        var page = Assert.Single(pdf.GetPages());
        Assert.Equal(KdpCoverSize.CoverWidth(pages) * 72, page.Width, 0.5);
        Assert.Equal(KdpCoverSize.CoverHeight * 72, page.Height, 0.5);
        Assert.True(result.Fonts.AllEmbedded);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Both_pictures_are_placed_and_the_credit_lines_come_from_the_back_cover_wording()
    {
        var result = KdpCover.Build(Input(24, withBack: true));

        using var pdf = PdfDocument.Open(result.Pdf);
        var page = pdf.GetPage(1);
        Assert.Equal(6, page.GetImages().Count()); // front, back, the QR code and a soft shadow each for the title, the credits and the Dino line
        var text = string.Join(' ', page.GetWords().Select(w => w.Text));
        foreach (var line in BackCoverWording.Lines(Credits, "en"))
        {
            foreach (var word in line.Split(' '))
            {
                Assert.Contains(word, text);
            }
        }

        Assert.Contains("Dino:", text);
    }

    [Fact]
    public void The_barcode_box_holds_no_picture_and_no_words()
    {
        var result = KdpCover.Build(Input(24, withBack: true));

        using var pdf = PdfDocument.Open(result.Pdf);
        var box = result.Barcode;
        var left = box.X * 72;
        var right = (box.X + box.Width) * 72;
        var top = result.HeightInches * 72 - box.Y * 72;
        var bottom = top - box.Height * 72;
        var inside = pdf.GetPage(1).Letters.Where(l => l.BoundingBox.Left < right && l.BoundingBox.Right > left && l.BoundingBox.Bottom < top && l.BoundingBox.Top > bottom);
        Assert.Empty(inside);
    }

    [Fact]
    public void A_very_long_title_is_shrunk_to_fit_and_the_cover_is_still_drawn()
    {
        var title = "Freddy and the Very Long Adventure of the Three Little Pigs in the Forest and the Big Bad Wolf";
        var input = Input(24, withBack: true) with { Title = title };

        var result = KdpCover.Build(input);

        using var pdf = PdfDocument.Open(result.Pdf);
        Assert.Contains("Forest", string.Join(' ', pdf.GetPage(1).GetWords().Select(w => w.Text)));
    }

    [Fact]
    public void A_right_to_left_book_has_its_barcode_box_lower_left_of_the_back_on_the_right_of_the_file()
    {
        var input = Input(24, withBack: true) with { Language = "fa", Direction = ReadingDirection.RightToLeft };

        var result = KdpCover.Build(input);

        var back = KdpCoverSize.BackPanel(24, ReadingDirection.RightToLeft);
        Assert.Equal(KdpCoverSize.Barcode(24, ReadingDirection.RightToLeft), result.Barcode);
        Assert.True(result.Barcode.X < back.X + back.Width / 2);
        Assert.True(back.X > KdpCoverSize.Spine(24).X);
        Assert.Equal(ReadingDirection.RightToLeft, result.Direction);
        using var pdf = PdfDocument.Open(result.Pdf);
        Assert.Equal(6, pdf.GetPage(1).GetImages().Count()); // front, back, the QR code and a soft shadow each for the title, the credits and the Dino line
    }

    [Fact]
    public void A_farsi_book_sets_its_title_in_vazirmatn_right_to_left()
    {
        var input = Input(24, withBack: true) with { Title = "ماجراهای فردی و دوستانش", Language = "fa", Direction = ReadingDirection.RightToLeft };

        var result = KdpCover.Build(input);

        Assert.True(result.Fonts.AllEmbedded);
        Assert.Contains(result.Fonts.Names, name => name.Contains("Vazirmatn", StringComparison.OrdinalIgnoreCase));
        using var pdf = PdfDocument.Open(result.Pdf);
        Assert.Contains(pdf.GetPage(1).Letters, l => l.Value is not " ");
    }

    [Fact]
    public void The_report_names_spine_text_by_the_page_count()
    {
        var short24 = KdpCheckReport.Build("T", "en", KdpInterior.Build(SyntheticBook.Input(3)), KdpCover.Build(Input(24, withBack: true)));
        var long80 = KdpCheckReport.Build("T", "en", KdpInterior.Build(SyntheticBook.Input(3)), KdpCover.Build(Input(80, withBack: true)));

        Assert.Contains("KDP needs at least 79 pages", short24, StringComparison.Ordinal);
        Assert.DoesNotContain("KDP needs at least 79 pages", long80, StringComparison.Ordinal);
    }

    [Fact]
    public void A_back_picture_without_credits_is_reported_as_a_picture_with_no_credit_lines()
    {
        var input = Input(24, withBack: true) with { Credits = null };

        var result = KdpCover.Build(input);

        Assert.True(result.BackPictureRead);
        Assert.False(result.CreditsRead);
        Assert.Equal([KdpCover.NoCreditsWarning], result.Warnings);
    }

    [Fact]
    public void Without_a_back_cover_the_back_is_plain_and_the_report_line_says_so()
    {
        var result = KdpCover.Build(Input(24, withBack: false));

        Assert.False(result.BackPictureRead);
        Assert.Equal([KdpCover.NoBackCoverWarning], result.Warnings);
        using var pdf = PdfDocument.Open(result.Pdf);
        Assert.Equal(2, pdf.GetPage(1).GetImages().Count()); // the front picture and its title's soft shadow
    }

    [Fact]
    public void The_export_writes_cover_pdf_and_the_report_lists_size_spine_and_barcode()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "book.dino"), pages: 3);
            var result = KdpExporter.Export(new KdpExportRequest(dino, "en", Path.Combine(folder, "out")));

            Assert.True(File.Exists(result.CoverPath));
            var report = File.ReadAllText(result.ReportPath);
            Assert.Contains("## Cover", report);
            Assert.Contains("Spine width: 0.0563 in for 24 pages", report);
            Assert.Contains("Barcode spot, kept free of words and the QR code", report);
            Assert.Contains(KdpCover.NoBackCoverWarning, report);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
