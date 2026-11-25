using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>Slice 46 item A: what QuestPDF can do for inline styled words (shifted, rotated, bouncing), right-to-left text with an injected word, and a round picture. Kept as real tests: they pin the API shapes items E, F and G build on.</summary>
public class KdpStyledTextSpikeTests
{
    private const float BodyPt = 24;
    private const float LineHeight = 1.35f;

    // An injected word sits against the line edge it is aligned to, not on the baseline of the words around it. Measured here at 24 pt in Andika
    // with 24 pt neighbours: aligned to the bottom (inner line height 1.35) it sits 0.032 em above their baseline, aligned to the top (inner line
    // height 1) it sits 0.203 em below it. OffsetY (it does not change the layout) takes that difference out. The difference is NOT proportional to
    // the size: with 28 and 30 pt words among 28 and 30 pt neighbours the top-aligned one sat 8.55 and 10.36 pt below (not 5.7 and 6.1), so these
    // two numbers are only good for 24 pt lines (the story size: R6 shapes move words of a 24 pt line). Words that only change size (R2, R6 loud
    // and quiet sounds, R7, R8) are spans and share the baseline; the song (R10) uses Bouncing, which needs no calibration.
    private const float BottomEm = 0.032f;
    private const float TopEm = 0.203f;

    private static Document Doc(Action<IContainer> content, string family = "Andika", string? background = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        KdpFonts.EnsureRegistered();
        return Document.Create(d => d.Page(p =>
        {
            p.Size(612, 612);
            p.Margin(61);
            if (background is not null)
            {
                p.PageColor(background);
            }

            p.DefaultTextStyle(s => s.FontFamily(family).FontSize(BodyPt).LineHeight(LineHeight).FontColor("#2B2540"));
            p.Content().Element(content);
        })).WithSettings(new DocumentSettings { CompressDocument = false });
    }

    private static SKBitmap Render(Document doc)
    {
        var png = doc.GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 144 }).Single();
        return SKBitmap.Decode(png);
    }

    /// <summary>A word set inline, <paramref name="up"/> pt above (positive) or below (negative) the baseline of its line; the line grows by that much on that side.</summary>
    private static void Shifted(TextDescriptor t, string word, float up, float pt = BodyPt, bool bold = false)
    {
        if (up >= 0)
        {
            t.Element(e => e.PaddingBottom(up).OffsetY(BottomEm * pt).Text(w => Styled(w.Span(word), pt, bold, LineHeight)), TextInjectedElementAlignment.Bottom);
        }
        else
        {
            t.Element(e => e.PaddingTop(-up).OffsetY(-TopEm * pt).Text(w => Styled(w.Span(word), pt, bold, 1f)), TextInjectedElementAlignment.Top);
        }
    }

    /// <summary>
    /// A song word (R10): every word of the line is set the same way, aligned to the bottom of the line, and a "high" word gets 4 pt of bottom padding, so
    /// high words sit exactly 4 pt (+2 / -2 about the middle) above low ones whatever the size, with nothing to calibrate against the words around them.
    /// </summary>
    private static void Bouncing(TextDescriptor t, string word, bool high, float pt)
    {
        t.Element(e => e.PaddingBottom(high ? 4 : 0).Text(w => Styled(w.Span(word), pt, bold: true, LineHeight)), TextInjectedElementAlignment.Bottom);
    }

    private static void Styled(TextSpanDescriptor span, float pt, bool bold, float lineHeight)
    {
        span.FontSize(pt).LineHeight(lineHeight);
        if (bold)
        {
            span.Bold();
        }
    }

    /// <summary>Extra height a box of <paramref name="width"/> by <paramref name="height"/> needs on each side once it is turned by <paramref name="degrees"/>.</summary>
    private static float ExtraHeightPerSide(float width, float height, float degrees)
    {
        var rad = Math.Abs(degrees) * MathF.PI / 180;
        return Math.Max(0, (width * MathF.Sin(rad) + height * MathF.Cos(rad) - height) / 2);
    }

    private static void Turned(TextDescriptor t, string word, float degrees, float widthPt)
    {
        var extra = ExtraHeightPerSide(widthPt, BodyPt * LineHeight, degrees);
        t.Element(e => e.PaddingVertical(extra).Rotate(degrees).Text(w => w.Span(word)), TextInjectedElementAlignment.Bottom);
    }

    private static IReadOnlyList<Letter> Letters(byte[] pdf, int page = 1)
    {
        using var doc = PdfDocument.Open(pdf);
        return doc.GetPage(page).Letters.ToList();
    }

    /// <summary>A word made of letters that appear nowhere else on the test pages, so its letters can be found in the PDF one by one.</summary>
    private const string Marked = "zxqwjk";

    private static double Base(IReadOnlyList<Letter> letters, string value, int occurrence = 0) => letters.Where(l => l.Value == value).ElementAt(occurrence).StartBaseLine.Y;

    private static byte[] OneInjected(float up, bool injected = true) => Doc(c => c.Text(t =>
    {
        t.Span("aaa ");
        if (injected)
        {
            Shifted(t, "BIG", up);
        }
        else
        {
            t.Span("BIG");
        }

        t.Span(" ccc\nsecond");
    })).GeneratePdf();

    [Fact]
    public void OffsetY_compiles_and_TranslateY_is_obsolete_in_this_QuestPDF()
    {
        // The slice named TranslateY; QuestPDF 2026.9.2 renamed it OffsetY (CS0618 on TranslateY). Shifted() above compiles with OffsetY only.
        var letters = Letters(OneInjected(-6));
        Assert.NotEmpty(letters);
    }

    [Fact]
    public void A_word_shifted_down_6_pt_sits_6_pt_below_its_neighbours_and_its_line_grows_by_6_pt()
    {
        var plain = Letters(OneInjected(0, injected: false));
        var down = Letters(OneInjected(-6));

        Assert.Equal(6, Base(down, "a") - Base(down, "B"), 0.8);
        var growth = (Base(down, "a") - Base(down, "s")) - (Base(plain, "a") - Base(plain, "s"));
        Assert.InRange(growth, 6, 7);
        Assert.Equal(Base(plain, "a"), Base(down, "a"), 0.1);
    }

    [Fact]
    public void A_word_shifted_up_6_pt_sits_6_pt_above_its_neighbours_and_its_line_grows_by_6_pt()
    {
        var plain = Letters(OneInjected(0, injected: false));
        var up = Letters(OneInjected(6));

        Assert.Equal(6, Base(up, "B") - Base(up, "a"), 0.8);
        var growth = (Base(up, "a") - Base(up, "s")) - (Base(plain, "a") - Base(plain, "s"));
        Assert.Equal(0, growth, 0.1);
        Assert.InRange(Base(plain, "a") - Base(up, "a"), 6, 7);
    }

    [Fact]
    public void Rotate_turns_the_word_about_its_own_centre_and_does_not_change_the_layout()
    {
        byte[] Pdf(bool turned) => Doc(c => c.Text(t =>
        {
            t.Span("aaa ");
            if (turned)
            {
                t.Element(e => e.Rotate(-4).Text(w => w.Span(Marked)), TextInjectedElementAlignment.Bottom);
            }
            else
            {
                t.Element(e => e.Text(w => w.Span(Marked)), TextInjectedElementAlignment.Bottom);
            }

            t.Span(" ccc\nsecond");
        })).GeneratePdf();

        var flat = Letters(Pdf(false)).Where(l => Marked.Contains(l.Value, StringComparison.Ordinal)).OrderBy(l => l.StartBaseLine.X).ToList();
        var turned = Letters(Pdf(true)).Where(l => Marked.Contains(l.Value, StringComparison.Ordinal)).OrderBy(l => l.StartBaseLine.X).ToList();
        Assert.Equal(Marked.Length, turned.Count);

        (double X, double Y) Centre(List<Letter> ls) => (ls.Average(l => l.StartBaseLine.X), ls.Average(l => l.StartBaseLine.Y));
        var (fx, fy) = Centre(flat);
        var (tx, ty) = Centre(turned);
        var width = flat.Max(l => l.EndBaseLine.X) - flat.Min(l => l.StartBaseLine.X);
        var slope = (turned.Last().StartBaseLine.Y - turned.First().StartBaseLine.Y) / (turned.Last().StartBaseLine.X - turned.First().StartBaseLine.X);

        Assert.True(Math.Abs(tx - fx) < 1 && Math.Abs(ty - fy) < 1.5, "the centre of the word stays put: the pivot is the centre");
        Assert.Equal(4, Math.Atan(slope) * 180 / Math.PI, 0.3);
        Assert.True(width * Math.Sin(4 * Math.PI / 180) > 2, "a corner pivot would have moved the centre by about this much");
    }

    [Fact]
    public void A_page_with_a_shifted_word_a_turned_word_and_a_bouncing_song_keeps_every_word_once_in_the_text_layer_and_no_line_touches_the_next()
    {
        var doc = Doc(c => c.Text(t =>
        {
            t.Span("The rock fell ");
            Shifted(t, "down", -6);
            t.Span(" and rolled\nThe old bridge began to ");
            Turned(t, "wobble", -4, 90);
            t.Span(" and shake\n");
            var up = true;
            foreach (var word in new[] { "la", "la", "la", "la" })
            {
                Bouncing(t, word, up, 28);
                t.Span(" ").FontSize(28).Bold();
                up = !up;
            }

            t.Span("\nThe end of the page");
        }));
        var pdf = doc.GeneratePdf();

        using var parsed = PdfDocument.Open(pdf);
        var words = parsed.GetPage(1).GetWords().Select(w => w.Text).ToList();
        foreach (var once in new[] { "rock", "down", "bridge", "end" })
        {
            Assert.Single(words, w => w == once);
        }

        // A turned word is one text run per letter in the PDF (PdfPig then lists its letters as separate words): each letter of it is there exactly once.
        var turnedLetters = parsed.GetPage(1).Letters.Where(l => Math.Abs(l.EndBaseLine.Y - l.StartBaseLine.Y) > 0.3).Select(l => l.Value).OrderBy(v => v).ToList();
        Assert.Equal("wobble".OrderBy(ch => ch).Select(ch => ch.ToString()), turnedLetters);

        Assert.Equal(4, words.Count(w => w == "la"));
        Assert.Equal(4, InkBands(Render(doc)).Count);
    }

    [Fact]
    public void A_shifted_word_in_a_24_pt_line_lands_within_a_quarter_point_of_the_shift_asked_for()
    {
        var letters = Letters(Doc(c => c.Text(t =>
        {
            t.Span("aaa ");
            Shifted(t, "zz", 3);
            t.Span(" ccc ");
            Shifted(t, "qq", -3);
            t.Span(" ddd");
        })).GeneratePdf());
        Assert.Equal(3, Base(letters, "z") - Base(letters, "a"), 0.25);
        Assert.Equal(-3, Base(letters, "q") - Base(letters, "a"), 0.25);
    }

    [Fact]
    public void A_song_line_bounces_its_words_4_pt_apart_and_the_line_grows_by_4_pt()
    {
        var doc = Doc(c => c.Text(t =>
        {
            t.Span("top line\n");
            var up = true;
            foreach (var word in new[] { "zz", "qq", "zz", "qq" })
            {
                Bouncing(t, word, up, 28);
                t.Span(" ").FontSize(28).Bold();
                up = !up;
            }

            t.Span("\nbottom line");
        }));
        var plain = Letters(Doc(c => c.Text("top line\nbottom line")).GeneratePdf());
        var letters = Letters(doc.GeneratePdf());

        var upBase = Base(letters, "z");
        var downBase = Base(letters, "q");
        Assert.Equal(4, upBase - downBase, 1.0);
        Assert.Equal(upBase, Base(letters, "z", 2), 0.1);
        Assert.Equal(downBase, Base(letters, "q", 2), 0.1);

        // The line grows upward for the +2 words and downward for the -2 words: the one above and the one below both end up further from it.
        var flat = Letters(Doc(c => c.Text(t =>
        {
            t.Span("top line\n");
            t.Span("zz qq zz qq").FontSize(28).Bold();
            t.Span("\nbottom line");
        })).GeneratePdf());
        var span = Base(letters, "t") - Base(letters, "b");
        var flatSpan = Base(flat, "t") - Base(flat, "b");
        Assert.InRange(span - flatSpan, 4, 6);
    }

    [Fact]
    public void A_Persian_line_with_an_injected_word_keeps_every_letter_once_and_the_words_in_right_to_left_order()
    {
        // Five words with no letter in common, so each letter can be found in the PDF and placed in its word. The PDF names the joined forms
        // (ﺮ, ﻢ ...), so letters are compared after NFKC normalisation.
        var words = new[] { "سلم", "دنی", "فرق", "هتج", "بکز" };
        var doc = Doc(c => c.ContentFromRightToLeft().Text(t =>
        {
            t.Span(words[0] + " " + words[1] + " ");
            t.Element(e => e.ContentFromRightToLeft().PaddingBottom(4).Text(w => w.Span(words[2]).FontFamily("Vazirmatn")), TextInjectedElementAlignment.Bottom);
            t.Span(" " + words[3] + " " + words[4]);
        }), family: "Vazirmatn");

        var letters = Letters(doc.GeneratePdf());
        var shown = letters.Select(l => (Ch: l.Value.Normalize(System.Text.NormalizationForm.FormKC).FirstOrDefault(), X: l.StartBaseLine.X)).Where(l => l.Ch != default && l.Ch != ' ').ToList();
        Assert.Equal(string.Concat(words).OrderBy(ch => ch), shown.Select(l => l.Ch).OrderBy(ch => ch));

        double Centre(string word) => shown.Where(l => word.Contains(l.Ch, StringComparison.Ordinal)).Average(l => l.X);
        var centres = words.Select(Centre).ToList();
        for (var i = 1; i < centres.Count; i++)
        {
            Assert.True(centres[i - 1] > centres[i], $"word {i} sits to the right of word {i + 1}");
        }
    }

    [Fact]
    public void A_round_picture_with_border_and_shadow_is_clipped_to_the_circle()
    {
        var picture = RedPicture();
        var doc = Doc(c => c.AlignCenter().AlignTop().Width(230).Height(230)
            .Shadow(new BoxShadowStyle { OffsetX = 0, OffsetY = 2, Blur = 6, Color = Colors.Black.WithAlpha(0.12f) })
            .CornerRadius(115)
            .Border(6).BorderColor(Colors.White).BorderAlignmentInside()
            .Image(picture), background: "#F3EEF8");

        using var page = Render(doc);
        // 2 px per pt; the picture box is centred on x = 306 and starts at y = 61 (page margin).
        var cx = 306 * 2;
        var top = 61 * 2;
        var cy = top + 115 * 2;
        var corner = page.GetPixel(cx - 112 * 2, top + 6);
        var centre = page.GetPixel(cx, cy);
        var ringLeft = page.GetPixel(cx - 112 * 2, cy);
        var outside = page.GetPixel(cx - 120 * 2, cy);
        var below = page.GetPixel(cx, top + 230 * 2 + 3);
        Assert.True(centre.Red > 200 && centre.Green < 60, "the picture shows in the middle");
        Assert.False(corner.Red > 200 && corner.Green < 60, "the square's corner is not picture: it is clipped round");
        Assert.True(ringLeft.Green > 240 && ringLeft.Blue > 240, "the 6 pt border is white");
        Assert.True(below.Red < 0xF3 - 2, "a soft shadow darkens the page below the circle");
    }

    [Fact]
    public void The_ink_band_check_can_fail_lines_set_too_tight_run_into_each_other()
    {
        var tight = Doc(c => c.DefaultTextStyle(s => s.LineHeight(0.6f)).Text("Hag gym\nBig jug\nHog pig"));

        Assert.True(InkBands(Render(tight)).Count < 3);
    }

    private static byte[] RedPicture()
    {
        using var bitmap = new SKBitmap(400, 400);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(220, 20, 20));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>The runs of rows that hold ink (darker than the page), top to bottom: one per set line when no line touches the next.</summary>
    private static List<(int From, int To)> InkBands(SKBitmap page)
    {
        var bands = new List<(int, int)>();
        var start = -1;
        for (var y = 0; y < page.Height; y++)
        {
            var ink = false;
            for (var x = 0; x < page.Width && !ink; x++)
            {
                ink = page.GetPixel(x, y).Red < 160;
            }

            if (ink && start < 0)
            {
                start = y;
            }
            else if (!ink && start >= 0)
            {
                bands.Add((start, y - 1));
                start = -1;
            }
        }

        return bands;
    }
}
