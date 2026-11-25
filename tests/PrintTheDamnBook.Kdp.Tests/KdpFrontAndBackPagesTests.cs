using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp.StyleSheet;
using PrintTheDamnBook.Kdp.Styling;
using QuestPDF.Fluent;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>Slice 46 item G: the redesigned title, credits, belongs-to, The End and Dino pages are drawn as the typography spec says, and item H's style sheet is written.</summary>
public class KdpFrontAndBackPagesTests
{
    private static readonly string[] Cast = ["Seagull", "Kathryn"];
    private static readonly DinoCredits Credits = new("Ashkan Sirous", false, "Ashkan Sirous", 2026, 10);

    private static KdpBookInput Book(string[]? cast = null, KdpBookSettings? settings = null)
    {
        var texts = new[] { "\"Ahoy!\" said Kathryn.", "\"Squawk!\" said Seagull.", "Pip sat down." };
        return SyntheticBook.Input(3) with { Texts = texts, Cast = cast, Settings = settings, Credits = Credits };
    }

    private static Page PageOf(PdfDocument pdf, KdpInteriorResult result, KdpPageRole role) => pdf.GetPage(result.Plan.Pages.First(p => p.Role == role).Position);

    private static string Words(Page page) => string.Join(' ', page.GetWords().Select(w => w.Text));

    [Fact]
    public void The_title_page_has_the_round_picture_and_the_last_word_of_the_title_at_72_pt()
    {
        var result = KdpInterior.Build(Book());

        using var pdf = PdfDocument.Open(result.Pdf);
        var page = PageOf(pdf, result, KdpPageRole.Title);
        var picture = Assert.Single(page.GetImages());
        Assert.InRange(picture.BoundingBox.Width, 229, 231);
        var big = page.Letters.Where(l => l.PointSize > 60).Select(l => l.Value).ToList();
        Assert.Equal("Test", string.Concat(big));
        Assert.All(page.Letters.Where(l => l.PointSize < 60), l => Assert.Equal(36, l.PointSize, 0.5));
        Assert.Equal("Freddy and the Test", Words(page));
    }

    [Fact]
    public void The_title_page_and_the_end_take_the_tints_of_the_first_and_the_last_text_page()
    {
        var result = KdpInterior.Build(Book());
        var drawing = KdpInterior.DrawingFor(Book(), result.Plan, result.Pictures, result.Stories);

        Assert.Equal(result.Pictures[0].Tint, drawing.Front!.TitleTint);
        Assert.Equal(result.Pictures[2].Tint, drawing.Front.EndTint);
    }

    [Fact]
    public void The_credits_page_has_the_labels_the_names_the_date_and_the_rights_line()
    {
        var result = KdpInterior.Build(Book());

        using var pdf = PdfDocument.Open(result.Pdf);
        var page = PageOf(pdf, result, KdpPageRole.Credits);
        Assert.Equal("Story by Ashkan Sirous Illustrations by Ashkan Sirous October 2026 © 2026 Elinsoft Ltd. All rights reserved.", Words(page));
        Assert.Empty(page.GetImages());
        var name = page.Letters.First(l => l.Value == "A" && l.PointSize > 19);
        Assert.Equal(20, name.PointSize, 0.5);
    }

    [Fact]
    public void The_belongs_to_page_has_its_heading_and_draw_label_and_a_dashed_brown_frame()
    {
        var input = Book();
        var result = KdpInterior.Build(input);
        var drawing = KdpInterior.DrawingFor(input, result.Plan, result.Pictures, result.Stories);

        using var pdf = PdfDocument.Open(result.Pdf);
        var page = PageOf(pdf, result, KdpPageRole.BelongsTo);
        Assert.Equal("This book belongs to Draw yourself here!", Words(page));
        Assert.Equal(28, page.Letters.First(l => l.Value == "T").PointSize, 0.5);

        var belongs = result.Plan.Pages.First(p => p.Role == KdpPageRole.BelongsTo);
        using var preview = SKBitmap.Decode(KdpInteriorDocument.RenderPagePreviews(drawing)[belongs.Position - 1]);
        var scale = preview.Width / (KdpTrim.PageWidthInches * 72);
        var x = (int)Math.Round((KdpMatterSpec.FrameInsetPt + KdpMatterSpec.FrameBorderPt / 2) * scale);
        var brown = Enumerable.Range(100, 400).Select(y => preview.GetPixel(x, (int)(y * scale))).Count(c => c.Red > 100 && c.Red < 200 && c.Green < 130 && c.Blue < 70);
        Assert.InRange(brown, 20, 300);
    }

    [Fact]
    public void The_end_is_72_pt_in_the_main_characters_colour()
    {
        var result = KdpInterior.Build(Book(Cast));

        using var pdf = PdfDocument.Open(result.Pdf);
        var page = PageOf(pdf, result, KdpPageRole.TheEnd);
        Assert.Equal("The End", Words(page));
        Assert.All(page.Letters, l => Assert.Equal(72, l.PointSize, 0.5));
        var wanted = result.Style!.SpeakerColours[result.Style.MainCharacter!];
        var c = page.Letters[0].Color.ToRGBValues();
        Assert.InRange(c.r * 255, Convert.ToInt32(wanted[1..3], 16) - 2, Convert.ToInt32(wanted[1..3], 16) + 2);
        Assert.InRange(c.g * 255, Convert.ToInt32(wanted[3..5], 16) - 2, Convert.ToInt32(wanted[3..5], 16) + 2);
    }

    [Fact]
    public void The_dino_page_lists_the_speaking_characters_in_their_colours_after_the_dino_words()
    {
        var result = KdpInterior.Build(Book(Cast));

        using var pdf = PdfDocument.Open(result.Pdf);
        var page = PageOf(pdf, result, KdpPageRole.Dino);
        var words = Words(page);
        Assert.StartsWith("Scan the code, or type dinoreader.sirous.uk Google Play · App Store · Microsoft Store Listen to this story in Dino: Kids Books & Stories.", words, StringComparison.Ordinal);
        Assert.EndsWith("Seagull Kathryn", words, StringComparison.Ordinal); // both speak once: the cast's order
        var address = page.Letters.First(l => l.Value == "d" && l.PointSize > 25);
        Assert.Equal(26, address.PointSize, 0.5);
        var kathryn = page.GetWords().Single(w => w.Text == "Kathryn").Letters[0];
        Assert.Equal(20, kathryn.PointSize, 0.5);
        var colour = kathryn.Color.ToRGBValues();
        Assert.InRange(colour.r * 255, 0x0E - 2, 0x0E + 2); // the second fallback colour, #0E6B78 (the first, #1F4FA0, is Seagull's)
    }

    private const string FarsiTitle = "مرغ دریایی و کلاه";
    private static readonly string[] FarsiNames = ["مرغ", "کاترین"];

    private static (KdpInteriorResult Result, KdpDrawing Drawing) FarsiDrawing()
    {
        var input = SyntheticBook.Input(3, language: "fa") with { Title = FarsiTitle, Credits = new DinoCredits("اشکان", false, "اشکان", 2026, 10) };
        var result = KdpInterior.Build(input);
        var drawing = KdpInterior.DrawingFor(input, result.Plan, result.Pictures, result.Stories);
        var speakers = FarsiNames.Select(n => new KdpSpeakerName(n, "#1F4FA0")).ToList();
        return (result, drawing with { Front = drawing.Front! with { Speakers = speakers } });
    }

    private static List<Letter> LettersOf(byte[] pdfBytes, KdpPlan plan, KdpPageRole role)
    {
        using var pdf = PdfDocument.Open(pdfBytes);
        return pdf.GetPage(plan.Pages.First(p => p.Role == role).Position).Letters.ToList();
    }

    [Fact]
    public void Farsi_names_title_and_credits_are_set_in_Vazirmatn()
    {
        var (result, drawing) = FarsiDrawing();
        var pdf = KdpInteriorDocument.CreateDocument(drawing).GeneratePdf();

        foreach (var role in new[] { KdpPageRole.Title, KdpPageRole.Credits, KdpPageRole.Dino })
        {
            var arabic = LettersOf(pdf, result.Plan, role).Where(l => l.Value.Length > 0 && l.Value[0] >= 0x0600 && l.Value[0] <= 0x06FF).ToList();
            Assert.NotEmpty(arabic);
            Assert.All(arabic, l => Assert.Contains("Vazirmatn", l.FontName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void A_right_to_left_book_runs_the_dino_page_names_right_to_left()
    {
        var (result, rtl) = FarsiDrawing();
        var ltr = rtl with { Direction = ReadingDirection.LeftToRight };

        var right = LettersOf(KdpInteriorDocument.CreateDocument(rtl).GeneratePdf(), result.Plan, KdpPageRole.Dino);
        var left = LettersOf(KdpInteriorDocument.CreateDocument(ltr).GeneratePdf(), result.Plan, KdpPageRole.Dino);

        Assert.NotEqual(string.Concat(left.OrderBy(l => l.BoundingBox.Left).Select(l => l.Value)), string.Concat(right.OrderBy(l => l.BoundingBox.Left).Select(l => l.Value)));
    }

    [Fact]
    public void A_saved_title_picture_changes_the_circle_and_the_fallback_warning_goes()
    {
        var plain = KdpInterior.Build(Book());
        var saved = KdpInterior.Build(Book(settings: KdpBookSettings.Empty.WithTitlePicture(new KdpTitlePicture(2, 0.5, 0.3, 0.6))));

        Assert.Contains(plain.Warnings, w => w.StartsWith("styles: no saved title picture (fallback)", StringComparison.Ordinal));
        Assert.DoesNotContain(saved.Warnings, w => w.Contains("title picture", StringComparison.Ordinal));
        using var a = PdfDocument.Open(plain.Pdf);
        using var b = PdfDocument.Open(saved.Pdf);
        var first = PageOf(a, plain, KdpPageRole.Title).GetImages().Single();
        var second = PageOf(b, saved, KdpPageRole.Title).GetImages().Single();
        Assert.NotEqual(first.RawBytes.ToArray(), second.RawBytes.ToArray());
    }

    [Fact]
    public void The_style_sheet_lists_each_speaker_with_lines_pages_and_colour_the_main_character_the_tints_and_the_title_picture_rule()
    {
        var input = Book(Cast);
        var result = KdpInterior.Build(input);

        var text = KdpStyleSheetWriter.Build(input, result);

        Assert.Contains("| Kathryn | 1 | 1 | #", text, StringComparison.Ordinal);
        Assert.Contains("| Seagull | 1 | 2 | #", text, StringComparison.Ordinal);
        Assert.Contains("Fallback (most lines): ", text, StringComparison.Ordinal);
        Assert.Contains("| 3 | #", text, StringComparison.Ordinal);
        Assert.Contains("title = <page> <cx>,<cy>,<d>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_style_sheet_says_so_when_nobody_speaks_and_is_written_and_removed_with_the_option()
    {
        var input = Book();
        var result = KdpInterior.Build(input);
        Assert.Contains("No speaker from the cast was found", KdpStyleSheetWriter.Build(input, result), StringComparison.Ordinal);

        var folder = Path.Combine(Path.GetTempPath(), "kdp-style-sheet-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = KdpStyleSheetWriter.Write(folder, input, result);
            Assert.True(File.Exists(path));
            KdpStyleSheetWriter.Remove(folder);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
