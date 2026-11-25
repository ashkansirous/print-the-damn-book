using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Kdp.Styling;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>Slice 46 items E and F: voices (R5), songs (R10) and shapes (R6) are drawn in the interior PDF, the choices are saved in <c>kdp.json</c>, and every fallback is a warning.</summary>
public class KdpVoicesSongsAndShapesTests
{
    private static readonly string[] Cast = ["Seagull", "Kathryn", "Izzy", "Billy"];
    private const string Voices = "\"Squawk, look!\" said Seagull. \"Ahoy!\" said Kathryn.";
    private const string Song = "\"Zing zeng zong,\" sang Izzy.";
    private const string Down = "Down, down, down swooped Seagull.";
    private const string Rumble = "His tummy went rumble, rumble, rumble!";

    private static KdpInteriorResult Build(string[] texts, string[]? cast = null, KdpBookSettings? settings = null, string language = "en")
    {
        var input = SyntheticBook.Input(texts.Length, language) with { Texts = texts, Cast = cast, Settings = settings };
        return KdpInterior.Build(input);
    }

    private static IReadOnlyList<Letter> Letters(KdpInteriorResult result, int story)
    {
        using var pdf = PdfDocument.Open(result.Pdf);
        return pdf.GetPage(result.Stories[story].Position).Letters.ToList();
    }

    private static (int R, int G, int B) Rgb(Letter letter)
    {
        var c = letter.Color.ToRGBValues();
        return ((int)Math.Round(c.r * 255), (int)Math.Round(c.g * 255), (int)Math.Round(c.b * 255));
    }

    private static bool IsNear((int R, int G, int B) c, string hex)
    {
        var want = (R: Convert.ToInt32(hex[1..3], 16), G: Convert.ToInt32(hex[3..5], 16), B: Convert.ToInt32(hex[5..7], 16));
        return Math.Abs(c.R - want.R) <= 2 && Math.Abs(c.G - want.G) <= 2 && Math.Abs(c.B - want.B) <= 2;
    }

    private static double Base(IReadOnlyList<Letter> letters, string value) => letters.Single(l => l.Value == value).StartBaseLine.Y;

    private static List<char> LettersOf(string text) => text.Where(char.IsLetter).OrderBy(c => c).ToList();

    private static List<char> LettersOf(IReadOnlyList<Letter> letters) => string.Concat(letters.Select(l => l.Value)).Where(char.IsLetter).OrderBy(c => c).ToList();

    [Fact]
    public void R5_a_quote_is_drawn_in_its_speakers_fallback_colour_and_the_narration_in_ink()
    {
        var result = Build([Voices], Cast);

        var letters = Letters(result, 0);
        var word = result.Stories[0].Styled!.Paragraphs[0].Runs.First(r => r.Text.Contains("look", StringComparison.Ordinal));
        Assert.Equal("#1F4FA0", word.Look.Color);
        Assert.Contains(letters, l => l.Value == "l" && IsNear(Rgb(l), "#1F4FA0"));
        Assert.Contains(letters, l => l.Value == "A" && IsNear(Rgb(l), "#0E6B78"));
        Assert.Contains(letters, l => l.Value == "s" && IsNear(Rgb(l), "#2B2540"));
    }

    [Fact]
    public void R5_a_saved_speaker_colour_is_used_instead_of_the_fallback()
    {
        var settings = KdpBookSettings.Empty.WithSpeaker("Seagull", "#2F6B34").WithSpeaker("Kathryn", "#8A4B12");

        var result = Build([Voices], Cast, settings);

        var letters = Letters(result, 0);
        Assert.Contains(letters, l => l.Value == "l" && IsNear(Rgb(l), "#2F6B34"));
        Assert.Contains(letters, l => l.Value == "A" && IsNear(Rgb(l), "#8A4B12"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("no saved colour", StringComparison.Ordinal));
    }

    [Fact]
    public void A_saved_colour_that_fails_the_rules_is_reported_and_the_fallback_palette_is_used()
    {
        var settings = KdpBookSettings.Empty.WithSpeaker("Seagull", "#9CB4E0");

        var result = Build([Voices], Cast, settings);

        Assert.True(result.Style!.RejectedSpeakers.ContainsKey("Seagull"));
        Assert.Contains(result.Warnings, w => w.Contains("saved colour of Seagull is refused", StringComparison.Ordinal));
        Assert.Equal("#1F4FA0", result.Style.SpeakerColours["Seagull"]);
    }

    [Fact]
    public void Fallbacks_are_warnings_in_the_check_report_text_and_a_saved_choice_removes_them()
    {
        var plain = Build([Voices, "\"Hello,\" she said."], Cast);
        var saved = Build([Voices], Cast, KdpBookSettings.Empty.WithSpeaker("Seagull", "#2F6B34").WithSpeaker("Kathryn", "#8A4B12").WithMainCharacter("Seagull", "most lines").WithTitlePicture(new KdpTitlePicture(1, 0.5, 0.5, 0.8)));

        Assert.Contains(plain.Warnings, w => w.StartsWith("styles: no saved title picture (fallback)", StringComparison.Ordinal));
        Assert.Contains(plain.Warnings, w => w.StartsWith("styles: no saved colour for Seagull, Kathryn (fallback palette used)", StringComparison.Ordinal));
        Assert.Contains(plain.Warnings, w => w.Contains("no saved main character (fallback): Seagull has the most lines", StringComparison.Ordinal));
        Assert.Contains(plain.Warnings, w => w.Contains("no speaker found from the cast for a quote on story page(s) 2", StringComparison.Ordinal));
        Assert.DoesNotContain(saved.Warnings, w => w.StartsWith("styles:", StringComparison.Ordinal));
        Assert.Equal("Seagull", saved.Style!.MainCharacter);
        Assert.Contains("styles: no saved main character", KdpCheckReport.Build("T", "en", plain), StringComparison.Ordinal);
    }

    [Fact]
    public void A_book_with_no_cast_or_another_language_gets_no_style_warnings()
    {
        var noCast = Build([Voices]);
        var french = Build([Voices], Cast, language: "fr");

        Assert.DoesNotContain(noCast.Warnings, w => w.StartsWith("styles:", StringComparison.Ordinal) && !w.Contains("title picture", StringComparison.Ordinal));
        Assert.DoesNotContain(french.Warnings, w => w.StartsWith("styles:", StringComparison.Ordinal) && !w.Contains("title picture", StringComparison.Ordinal));
        Assert.Null(french.Style!.Voices);
    }

    [Fact]
    public void R10_a_sung_quote_is_28_pt_and_its_words_alternate_4_pt_apart_with_every_letter_once()
    {
        var result = Build([Song], Cast);

        var letters = Letters(result, 0);
        Assert.Equal(28, letters.Single(l => l.Value == "i").PointSize, 0.1);
        Assert.Equal(4, Base(letters, "i") - Base(letters, "e"), 1.0);
        Assert.Equal(Base(letters, "i"), Base(letters, "o"), 0.5);
        Assert.Equal(LettersOf(Song), LettersOf(letters));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void R6_downward_words_sit_3_and_6_pt_lower_than_the_first_and_every_word_is_there_once()
    {
        var result = Build([Down]);

        using var pdf = PdfDocument.Open(result.Pdf);
        var words = pdf.GetPage(result.Stories[0].Position).GetWords().Where(w => w.Text.StartsWith("down", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(3, words.Count);
        var first = words[0].Letters[0].StartBaseLine.Y;
        Assert.Equal(-3, words[1].Letters[0].StartBaseLine.Y - first, 0.8);
        Assert.Equal(-6, words[2].Letters[0].StartBaseLine.Y - first, 0.8);
        Assert.Equal(LettersOf(Down), LettersOf(Letters(result, 0)));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void R6_shaking_words_are_turned_in_the_pdf_and_their_letters_are_all_there_once()
    {
        var result = Build([Rumble]);

        var letters = Letters(result, 0);
        var turned = letters.Where(l => Math.Abs(l.EndBaseLine.Y - l.StartBaseLine.Y) > 0.3).ToList();
        Assert.Equal(21, turned.Count);
        Assert.Equal(LettersOf(Rumble), LettersOf(letters));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void R6_loud_sound_words_grow_in_the_pdf()
    {
        var result = Build(["The cow went moo, moo, moo in the field."]);

        var sizes = Letters(result, 0).Where(l => l.Value == "m" || l.Value == "o").Select(l => Math.Round(l.PointSize)).Distinct().Order().ToList();
        Assert.Contains(27d, sizes);
        Assert.Contains(30d, sizes);
    }

    [Fact]
    public void A_page_with_voices_a_song_and_shapes_together_fits_and_keeps_every_letter_once()
    {
        var page = Voices + "\n\n" + Song + "\n\n" + Down + "\n\n" + Rumble;

        var result = Build([page], Cast);

        Assert.Empty(result.Errors);
        Assert.Equal(LettersOf(page), LettersOf(Letters(result, 0)));
    }

    [Fact]
    public void A_page_too_crowded_once_its_words_are_moved_and_turned_is_still_an_overflow_error_never_shrunk()
    {
        var crowded = string.Join("\n\n", Enumerable.Repeat(Rumble + " " + Down, 7));

        var result = Build([crowded]);

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Stories, s => Assert.Equal(24, s.Pt));
    }
}
