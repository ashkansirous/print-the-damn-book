using PrintTheDamnBook.Core.Kdp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>Slice 46 items B to D: the interior draws styled story text (paragraph gaps, bold and italic fonts, bigger words) with excerpts of X Marks the Treasure!, and measures the real styled words.</summary>
public class KdpStyledStoryTextTests
{
    private const string Page1 = "Seagull flew high, high up over the sea. Down below she could see little boats, a sandy beach and a sunny harbour.\n\nThen she spotted a big pirate hat she knew. It was Captain Kathryn! She was running to her ship, waving a piece of paper. She looked so happy!";
    private const string Page2 = "Down, down, down swooped Seagull, and she landed right on top of Kathryn's big hat.\n\n\"Squawk! Squawk! Squawk! What's up, Kathryn?\" said Seagull.";
    private const string Page3 = "\"Squawk, squawk, squawk,\" said Seagull softly, and she snuggled down on Kathryn's big hat.";

    private static KdpInteriorResult Build(params string[] texts)
    {
        var input = SyntheticBook.Input(texts.Length) with { Texts = texts };
        return KdpInterior.Build(input);
    }

    private static IReadOnlyList<Letter> StoryLetters(KdpInteriorResult result, int story)
    {
        using var pdf = PdfDocument.Open(result.Pdf);
        return pdf.GetPage(result.Stories[story].Position).Letters.ToList();
    }

    [Fact]
    public void A_book_of_X_Marks_excerpts_builds_with_no_overflow_and_embeds_the_bold_and_italic_fonts()
    {
        var result = Build(Page1, Page2, Page3);

        Assert.Empty(result.Errors);
        Assert.True(result.Fonts.AllEmbedded);
        var names = StoryLetters(result, 0).Concat(StoryLetters(result, 2)).Select(l => l.FontName ?? string.Empty).Distinct().ToList();
        Assert.Contains(names, n => n.Contains("Bold", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, n => n.Contains("Italic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void R1_the_gap_between_two_paragraphs_is_14_pt_more_than_between_two_lines()
    {
        var gap = Build("Pip ran.\n\nPop hid.");
        var wrapped = Build("Pip ran and Pop hid and Pim went home to bake a nice warm cake for tea at six.");

        var spaced = LineStep(StoryLetters(gap, 0));
        var tight = LineStep(StoryLetters(wrapped, 0));

        Assert.Equal(14, spaced - tight, 0.6);
    }

    /// <summary>The distance between the baselines of the first and the second line of the 24 pt words.</summary>
    private static double LineStep(IReadOnlyList<Letter> letters)
    {
        var lines = letters.Where(l => Math.Abs(l.PointSize - 24) < 0.1).Select(l => Math.Round(l.StartBaseLine.Y, 1)).Distinct().OrderByDescending(y => y).ToList();
        return lines[0] - lines[1];
    }

    [Fact]
    public void R2_a_big_word_is_30_pt_and_a_small_word_is_19_pt_in_the_pdf()
    {
        var result = Build(Page1, Page2, Page3);

        var sizes = StoryLetters(result, 0).Select(l => Math.Round(l.PointSize)).Distinct().ToList();
        Assert.Contains(30d, sizes);
        Assert.Contains(19d, sizes);
        Assert.Contains(24d, sizes);
    }

    [Fact]
    public void Every_story_word_is_in_the_text_layer_exactly_once()
    {
        var result = Build(Page1, Page2, Page3);

        using var pdf = PdfDocument.Open(result.Pdf);
        var words = pdf.GetPage(result.Stories[0].Position).GetWords().Select(w => w.Text).ToList();
        foreach (var once in new[] { "Seagull", "pirate", "Captain", "harbour" })
        {
            Assert.Single(words, w => w.StartsWith(once, StringComparison.Ordinal));
        }

        var second = pdf.GetPage(result.Stories[1].Position).GetWords().Select(w => w.Text).ToList();
        Assert.Equal(3, second.Count(w => w.Contains("Squawk", StringComparison.Ordinal)));
    }

    [Fact]
    public void R4_triplet_words_are_drawn_in_the_triplet_colours()
    {
        var result = Build(Page2);

        var letters = StoryLetters(result, 0).Where(l => l.PointSize > 20).ToList();
        var colours = letters.Select(l => l.Color.ToRGBValues()).Select(c => ((int)Math.Round(c.r * 255), (int)Math.Round(c.g * 255), (int)Math.Round(c.b * 255))).Distinct().ToList();
        foreach (var hex in new[] { "#C2410C", "#1F5FA8", "#7A3E9D" })
        {
            Assert.Contains(colours, c => IsNear(c, hex));
        }
    }

    /// <summary>The PDF keeps a colour to two decimals, so a channel can be a step off its hex value.</summary>
    private static bool IsNear((int R, int G, int B) c, string hex)
    {
        var want = (R: Convert.ToInt32(hex[1..3], 16), G: Convert.ToInt32(hex[3..5], 16), B: Convert.ToInt32(hex[5..7], 16));
        return Math.Abs(c.R - want.R) <= 2 && Math.Abs(c.G - want.G) <= 2 && Math.Abs(c.B - want.B) <= 2;
    }

    [Fact]
    public void A_page_whose_styled_words_do_not_fit_is_a_report_error_never_shrunk()
    {
        var crowded = string.Join(' ', Enumerable.Repeat("The huge enormous giant big bears came", 22));

        var result = Build(crowded);

        Assert.Contains(result.Stories, s => !s.Fits);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Stories, s => Assert.Equal(24, s.Pt));
    }

    [Fact]
    public void Right_to_left_a_Farsi_page_with_a_triplet_is_drawn_bold_in_Vazirmatn()
    {
        var persian = "مرغ دریایی اومد پایین پایین پایین و نشست درست رو کلاه.\n\nقاق قاق قاق! چی شده کاترین؟";

        var result = KdpInterior.Build(SyntheticBook.Input(2, language: "fa", longText: persian));

        Assert.All(result.Stories, s => Assert.True(s.Fits));
        var names = StoryLetters(result, 0).Select(l => l.FontName ?? string.Empty).Distinct().ToList();
        Assert.Contains(names, n => n.Contains("Vazirmatn", StringComparison.OrdinalIgnoreCase) && n.Contains("Bold", StringComparison.OrdinalIgnoreCase));
    }
}
