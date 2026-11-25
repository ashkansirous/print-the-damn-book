using PrintTheDamnBook.Core.Kdp.Styling;
using static PrintTheDamnBook.Core.Tests.Kdp.Styling.StyleTestSupport;

namespace PrintTheDamnBook.Core.Tests.Kdp.Styling;

/// <summary>Slice 46 item E: R5 voices (speakers from the cast), R10 songs and the speaker analysis, with excerpts of five catalog books.</summary>
public class VoiceAndSongRuleTests
{
    private static readonly string[] XMarksCast = ["Seagull", "Kathryn", "Izzy", "Billy", "Pancake Polly"];
    private const string SeagullBlue = "#1F4FA0";
    private const string KathrynGreen = "#2F6B34";

    private static StoryVoices Voices(params string[] cast) => StoryVoices.For(cast, "en")!;

    private static IReadOnlyList<StoryQuote> Quotes(string text, params string[] cast)
    {
        return StoryQuotes.Find(StoryTokens.Split(text), Voices(cast));
    }

    private static StyledPage Voiced(string text, params (string Name, string Hex)[] colours)
    {
        var voices = Voices(XMarksCast.Concat(["Gingerbread Man", "Granny", "Wolf", "Red Riding Hood", "Pip", "Mother Duck"]).ToArray());
        return new StoryStyler([text], "en", voices, colours.ToDictionary(c => c.Name, c => c.Hex)).Style(0, null);
    }

    [Fact]
    public void A_quote_is_attributed_to_the_cast_name_after_its_speech_verb()
    {
        var quote = Assert.Single(Quotes("\"Look! I've found the map,\" said Kathryn.", XMarksCast));

        Assert.Equal("Kathryn", quote.Speaker);
        Assert.False(quote.IsSong);
    }

    [Fact]
    public void A_cast_name_of_two_words_is_found_whole()
    {
        var quote = Assert.Single(Quotes("\"Seagull! This letter is for you!\" said Pancake Polly.", XMarksCast));

        Assert.Equal("Pancake Polly", quote.Speaker);
    }

    [Fact]
    public void A_name_before_the_verb_works_on_either_side_of_the_quote()
    {
        Assert.Equal("Seagull", Assert.Single(Quotes("\"Squawk,\" Seagull said.", XMarksCast)).Speaker);
        Assert.Equal("Kathryn", Assert.Single(Quotes("Kathryn said, \"Ahoy, pirates!\"", XMarksCast)).Speaker);
    }

    [Fact]
    public void The_attribution_of_a_split_quote_covers_both_halves()
    {
        var quotes = Quotes("\"Hmm,\" said Seagull, \"I'll go and see what's wrong!\"", XMarksCast);

        Assert.Equal(2, quotes.Count);
        Assert.All(quotes, q => Assert.Equal("Seagull", q.Speaker));
    }

    [Fact]
    public void A_pronoun_or_no_attribution_leaves_the_speaker_unknown()
    {
        var pronoun = Assert.Single(Quotes("\"Hello,\" she said.", XMarksCast));
        var none = Assert.Single(Quotes("And there was a sign. \"X marks the treasure!\"", XMarksCast));

        Assert.Null(pronoun.Speaker);
        Assert.True(pronoun.HasVerb);
        Assert.Null(none.Speaker);
        Assert.False(none.HasVerb);
    }

    [Fact]
    public void A_name_in_the_sentence_before_a_quote_is_not_its_speaker()
    {
        var quote = Assert.Single(Quotes("Seagull flew down the steps. \"Squawk!\"", XMarksCast));

        Assert.Null(quote.Speaker);
    }

    [Fact]
    public void A_cast_name_after_other_words_of_the_clause_is_not_next_to_the_verb()
    {
        var quote = Assert.Single(Quotes("\"Hello,\" she said to Seagull.", XMarksCast));

        Assert.Null(quote.Speaker);
    }

    [Fact]
    public void Sang_and_sings_make_a_song_and_said_does_not()
    {
        Assert.True(Assert.Single(Quotes("\"Yo ho, yo ho!\" sang Izzy.", XMarksCast)).IsSong);
        Assert.True(Assert.Single(Quotes("Billy sings, \"Yo ho!\"", XMarksCast)).IsSong);
        Assert.False(Assert.Single(Quotes("\"Yo ho, yo ho!\" said Izzy.", XMarksCast)).IsSong);
    }

    [Fact]
    public void X_marks_page_2_gives_the_squawk_to_seagull_and_only_the_last_quote_has_no_speaker()
    {
        var byParagraph = XMarksSquawk.Split('\n').Where(p => p.Length > 0).SelectMany(p => Quotes(p, XMarksCast)).ToList();

        Assert.Equal(["Seagull", "Kathryn", null], byParagraph.Select(q => q.Speaker));
    }

    [Fact]
    public void R5_the_words_of_a_quote_take_the_speakers_colour_and_the_narration_stays_ink()
    {
        var page = Voiced(XMarksSquawk, ("Seagull", SeagullBlue), ("Kathryn", KathrynGreen));

        Assert.Equal(SeagullBlue, LookOf(page, "What's").Color);
        Assert.Equal(KathrynGreen, LookOf(page, "found").Color);
        Assert.Equal(StyleColors.Ink, LookOf(page, "swooped").Color);
        Assert.Equal(StyleColors.Ink, LookOf(page, "said").Color);
        Assert.Equal(StyleColors.Ink, LookOf(page, "held").Color);
    }

    [Fact]
    public void R5_the_quote_marks_take_the_colour_of_their_words()
    {
        var page = Voiced("\"Look!\" said Kathryn.", ("Kathryn", KathrynGreen));

        var first = Runs(page)[0];
        Assert.StartsWith("\"", first.Text, StringComparison.Ordinal);
        Assert.Equal(KathrynGreen, first.Look.Color);
        Assert.Contains("!\"", first.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void R5_a_speaker_without_a_colour_and_an_unknown_speaker_stay_in_ink()
    {
        var page = Voiced("\"Hello,\" she said. \"Aye!\" said Izzy.", ("Kathryn", KathrynGreen));

        Assert.Equal(StyleColors.Ink, LookOf(page, "Hello").Color);
        Assert.Equal(StyleColors.Ink, LookOf(page, "Aye").Color);
    }

    [Fact]
    public void R5_never_changes_a_word_and_a_language_without_a_word_list_skips_voices_quietly()
    {
        var voices = StoryVoices.For(XMarksCast, "fa");
        var page = new StoryStyler([FarsiSquawk], "fa", voices, new Dictionary<string, string> { ["Seagull"] = SeagullBlue }).Style(0, null);

        Assert.Null(voices);
        Assert.Equal(FarsiSquawk, page.Text);
        Assert.DoesNotContain(Runs(page), r => r.Look.Color == SeagullBlue);
    }

    [Fact]
    public void R5_works_on_the_other_catalog_books()
    {
        var ginger = Voiced(GingerStop, ("Granny", KathrynGreen));
        var red = Voiced(RedBigEyes, ("Wolf", SeagullBlue), ("Red Riding Hood", KathrynGreen));
        var pigs = Voiced(PigsYay, ("Pip", SeagullBlue));
        var duck = Voiced(DuckGreatBig, ("Mother Duck", SeagullBlue));

        Assert.Equal(KathrynGreen, LookOf(ginger, "Stop").Color);
        Assert.Equal(StyleColors.Ink, LookOf(ginger, "cuddle").Color);
        Assert.Equal(KathrynGreen, LookOf(red, "eyes").Color);
        Assert.Equal(SeagullBlue, LookOf(red, "better").Color);
        Assert.Equal(SeagullBlue, LookOf(pigs, "play").Color);
        Assert.Equal(SeagullBlue, LookOf(duck, "baby").Color);
    }

    [Fact]
    public void R10_a_sung_quote_is_28_pt_bold_in_the_singers_colour_with_words_bouncing_2_pt_up_and_down()
    {
        var page = Voiced("\"Yo ho, yo ho!\" sang Izzy.", ("Izzy", SeagullBlue));

        var song = Runs(page).Where(r => r.Look.Song).ToList();
        Assert.Equal(4, song.Count);
        Assert.All(song, r => Assert.Equal(28, r.Look.Pt));
        Assert.All(song, r => Assert.True(r.Look.Bold));
        Assert.All(song, r => Assert.Equal(SeagullBlue, r.Look.Color));
        Assert.Equal([2.0, -2.0, 2.0, -2.0], song.Select(r => r.Look.BaselinePt));
        Assert.DoesNotContain(Runs(page), r => r.Text.Contains("sang", StringComparison.Ordinal) && r.Look.Song);
    }

    [Fact]
    public void R10_a_song_beats_a_refrain_and_a_shout_for_size_and_keeps_every_word_once()
    {
        var song = "\"Yo ho, yo ho!\" sang Izzy.";
        var voices = Voices(XMarksCast);
        var styler = new StoryStyler([song, song], "en", voices, new Dictionary<string, string> { ["Izzy"] = SeagullBlue });

        var page = styler.Style(0, null);
        Assert.Contains("yo ho yo ho", styler.Refrains);
        Assert.All(Runs(page).Where(r => r.Look.Song), r => Assert.Equal(28, r.Look.Pt));
        Assert.Equal(song, page.Text);
    }

    [Fact]
    public void R7_an_attribution_that_repeats_is_never_a_refrain_in_the_styler()
    {
        var voices = Voices(XMarksCast);
        var book = new[] { "\"Yo ho!\" sang Pancake Polly.", "\"Hey ho!\" sang Pancake Polly.", "\"Ho!\" said Seagull softly.", "\"Hi!\" said Seagull softly." };

        var styler = new StoryStyler(book, "en", voices);

        Assert.Empty(styler.Refrains);
    }

    [Fact]
    public void The_analysis_counts_each_speakers_lines_and_pages_most_talkative_first()
    {
        var pages = new[] { XMarksSquawk, "\"Yo ho!\" sang Izzy.\n\n\"Yo ho!\" sang Billy.", "\"Hello,\" said Kathryn. \"Hi,\" she said." };

        var analysis = SpeakerAnalysis.Analyse(pages, Voices(XMarksCast));

        Assert.Equal(["Kathryn", "Seagull", "Izzy", "Billy"], analysis.Speakers.Select(s => s.Name));
        Assert.Equal(2, analysis.Speakers[0].Lines);
        Assert.Equal([1, 3], analysis.Speakers[0].Pages);
        Assert.Equal("Kathryn", analysis.MostLines!.Name);
        Assert.Equal([1, 3], analysis.PagesWithUnknownSpeaker);
        Assert.True(analysis.HasSongs);
    }

    [Fact]
    public void A_tie_keeps_the_order_of_the_cast()
    {
        var analysis = SpeakerAnalysis.Analyse(["\"A!\" said Billy.", "\"B!\" said Seagull."], Voices(XMarksCast));

        Assert.Equal(["Seagull", "Billy"], analysis.Speakers.Select(s => s.Name));
    }

    [Fact]
    public void A_book_with_no_cast_or_no_word_list_has_no_speakers_and_no_warning_pages()
    {
        Assert.Same(SpeakerAnalysis.None, SpeakerAnalysis.Analyse([XMarksSquawk], StoryVoices.For([], "en")));
        Assert.Null(StoryVoices.For(XMarksCast, "ar"));
        Assert.Empty(SpeakerAnalysis.None.Speakers);
    }

    [Fact]
    public void Quotes_without_attribution_in_arabic_script_are_skipped_without_throwing()
    {
        var page = Style(ArabicSquawk, "ar");

        Assert.Equal(ArabicSquawk, page.Text);
    }
}
