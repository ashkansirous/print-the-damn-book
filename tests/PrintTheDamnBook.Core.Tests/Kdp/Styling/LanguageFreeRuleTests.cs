using PrintTheDamnBook.Core.Kdp.Styling;
using static PrintTheDamnBook.Core.Tests.Kdp.Styling.StyleTestSupport;

namespace PrintTheDamnBook.Core.Tests.Kdp.Styling;

public class LanguageFreeRuleTests
{
    private static IReadOnlyList<(string Text, string Color)> TripletWords(string text)
    {
        var tokens = StoryTokens.Split(text);
        return StoryTriplets.Find(tokens).SelectMany(t => new[] { t.First, t.Second, t.Third }).Select((i, n) => (tokens[i].Text, StyleSpec.TripletColors[n % 3])).ToList();
    }

    [Fact]
    public void R4_the_same_word_three_times_in_a_row_is_a_triplet()
    {
        Assert.Single(StoryTriplets.Find(StoryTokens.Split("Knock, knock, knock!")));
        Assert.Single(StoryTriplets.Find(StoryTokens.Split(PigsKnock.Split('\n')[0])));
    }

    [Fact]
    public void R4_colours_go_in_order_and_the_words_are_bold()
    {
        var page = Style(XMarksSquawk.Split('\n')[0]);

        Assert.Equal(new RunLook(24, Bold: true, Color: "#C2410C"), LookOf(page, "Down"));
        Assert.Equal("#1F5FA8", LookOf(page, "down").Color);
        Assert.Equal(3, Runs(page).Count(r => r.Look.Color is "#C2410C" or "#1F5FA8" or "#7A3E9D"));
    }

    [Fact]
    public void R4_splish_splash_splosh_is_a_triplet_because_they_share_two_letters_at_each_end()
    {
        Assert.True(StoryTriplets.Alike("splish", "splash", "splosh"));
        Assert.Single(StoryTriplets.Find(StoryTokens.Split("Splish, splash, splosh went the water.")));
    }

    [Fact]
    public void R4_a_hyphenated_word_said_three_times_is_a_triplet()
    {
        Assert.Single(StoryTriplets.Find(StoryTokens.Split(GingerPitter.Split(". ")[1])));
        Assert.Single(StoryTriplets.Find(StoryTokens.Split(DuckSplash.Split(" And")[0])));
    }

    [Fact]
    public void R4_two_in_a_row_or_short_different_words_are_not_triplets()
    {
        Assert.Empty(StoryTriplets.Find(StoryTokens.Split("flew high, high up over the sea")));
        Assert.Empty(StoryTriplets.Find(StoryTokens.Split(PigsNames)));
        Assert.Empty(StoryTriplets.Find(StoryTokens.Split("far, far away")));
        Assert.False(StoryTriplets.Alike("pip", "pop", "pim"));
    }

    [Fact]
    public void R4_two_triplets_in_one_paragraph_are_both_found()
    {
        Assert.Equal(2, StoryTriplets.Find(StoryTokens.Split(RedHaHa)).Count);
        Assert.Equal(6, TripletWords(RedHaHa).Count);
    }

    [Fact]
    public void R4_words_belong_to_one_triplet_only()
    {
        Assert.Single(StoryTriplets.Find(StoryTokens.Split("la la la la")));
    }

    [Fact]
    public void R7_a_clause_found_twice_in_the_book_is_30_pt_bold()
    {
        var book = new[] { GingerQuote, "Soon he met a Cow.\n\n" + GingerQuote, "He ran. " + GingerQuote };
        var styler = new StoryStyler(book, "en");

        var page = styler.Style(0, null);
        Assert.Contains("run run as fast as you can", styler.Refrains);
        Assert.Equal(new RunLook(30, Bold: true), LookOf(page, "fast"));
        Assert.Equal(new RunLook(30, Bold: true), LookOf(page, "Gingerbread"));
        Assert.True(IsPlain(Runs(styler.Style(1, null)).First(r => r.Text.Contains("Cow", StringComparison.Ordinal)).Look));
    }

    [Fact]
    public void R7_an_attribution_clause_of_three_words_is_never_a_refrain_when_the_cast_is_known()
    {
        // Slice 46 decision 4 (desk, during the build): "said Gingerbread Man" and "sang Pancake Polly" (a speech verb next to a cast name) are never refrains.
        var voices = StoryVoices.For(["Gingerbread Man", "Pancake Polly"], "en");
        var styler = new StoryStyler([GingerRun, GingerRun, "\"Yo ho!\" sang Pancake Polly.", "\"Hey!\" sang Pancake Polly."], "en", voices);

        Assert.DoesNotContain("said gingerbread man", styler.Refrains);
        Assert.DoesNotContain("sang pancake polly", styler.Refrains);
        Assert.Contains("run run as fast as you can", styler.Refrains);
    }

    [Fact]
    public void R7_without_a_cast_nothing_is_known_to_be_an_attribution_so_the_clause_compares_as_written()
    {
        var styler = new StoryStyler([GingerRun, GingerRun], "en");

        Assert.Contains("said gingerbread man", styler.Refrains);
    }

    [Fact]
    public void R7_commas_stay_inside_the_clause_and_a_clause_found_once_is_not_a_refrain()
    {
        var book = new[] { GingerQuote, "One page only: the old map was torn." };
        var styler = new StoryStyler(book, "en");

        Assert.DoesNotContain("run run as fast as you can", styler.Refrains);
        Assert.True(IsPlain(LookOf(styler.Style(1, null), "old")));
    }

    [Fact]
    public void R7_two_word_attributions_never_count()
    {
        var book = new[] { "\"Look!\" said Kathryn.", "\"Wow!\" said Kathryn.", "\"Aye!\" said Kathryn." };
        var styler = new StoryStyler(book, "en");

        Assert.Empty(styler.Refrains);
    }

    [Fact]
    public void R7_matches_ignoring_case_and_punctuation_and_needs_no_word_list()
    {
        var styler = new StoryStyler(["Run, run, as fast as you can!", "run run AS FAST as you can."], "fr");

        Assert.Single(styler.Refrains);
        Assert.Equal(30, LookOf(styler.Style(1, null), "FAST").Pt);
    }

    [Fact]
    public void R8_one_to_three_words_that_start_a_quote_and_end_in_an_exclamation_mark_are_30_pt_bold()
    {
        var page = Style(XMarksSquawk);

        // "Squawk! Squawk! Squawk!" is a loud-sound triplet, whose R6 sizes (24/27/30) win over R8 (R6 is last in the precedence): see ShapeRuleTests.
        Assert.Equal(new RunLook(30, Bold: true), LookOf(page, "Look"));
        Assert.Equal(new RunLook(30, Bold: true), LookOf(Style(PigsYay), "Yay"));
    }

    [Fact]
    public void R8_the_exclamation_mark_after_the_shout_is_bold_and_big_too_and_the_next_word_is_plain()
    {
        var page = Style(PigsYay);

        var shout = Runs(page).First(r => r.Text.Contains("Yay", StringComparison.Ordinal));
        Assert.Equal(30, shout.Look.Pt);
        Assert.Equal("\"Yay!", shout.Text);
        Assert.True(IsPlain(LookOf(page, "Now")));
    }

    [Fact]
    public void R8_later_shouts_in_the_same_quote_and_words_after_a_comma_stay_plain()
    {
        var page = Style("\"Look! Land! What a day!\" said Kathryn.");

        var shouts = Runs(page).Where(r => r.Text.Contains("Look", StringComparison.Ordinal) || r.Text.Contains("Land", StringComparison.Ordinal)).ToList();
        Assert.Equal([30d, 24d], shouts.Select(r => r.Look.Pt).ToArray());
        Assert.Equal(24, LookOf(Style(GingerStop), "Stop").Pt);
        Assert.True(IsPlain(LookOf(Style(GingerStop), "Stop")));
    }

    [Fact]
    public void R8_a_quote_that_does_not_end_in_an_exclamation_mark_is_not_a_shout()
    {
        var page = Style(XMarksSoftly);

        Assert.True(LookOf(page, "Squawk").Bold, "the three squawks are a triplet (R4), not a shout");
        Assert.Equal(24, LookOf(page, "Squawk").Pt);
    }

    [Fact]
    public void R8_four_words_are_too_many_for_a_shout()
    {
        var page = Style("\"Oh my, what a big baby!\" said Mum. \"One two three four!\" said Mum.");

        Assert.Equal(24, LookOf(page, "four").Pt);
    }

    [Fact]
    public void Precedence_R3_wins_the_colour_over_R7_and_R4_wins_over_R3()
    {
        var refrain = "Look at the green frog hop away";
        var styler = new StoryStyler([refrain, refrain], "en");
        var look = LookOf(styler.Style(0, White), "green");
        Assert.Equal(30, look.Pt);
        Assert.True(look.Bold);
        Assert.NotEqual("#2B2540", look.Color);

        var colours = Runs(Style("A red red red car.", tint: White)).Where(r => r.Look.Bold).Select(r => r.Look.Color).ToArray();
        Assert.Equal(["#C2410C", "#1F5FA8", "#7A3E9D"], colours.Take(3).ToArray());
    }

    [Fact]
    public void Precedence_R2_size_stays_when_R4_colours_the_word()
    {
        var page = Style("It was big, big, big!");

        Assert.Equal(new RunLook(30, Bold: true, Color: "#C2410C"), LookOf(page, "big"));
    }

    [Fact]
    public void Right_to_left_a_Farsi_triplet_is_found_and_the_words_are_kept()
    {
        var page = Style(FarsiTriplet, "fa");

        Assert.Equal(FarsiTriplet, page.Text);
        var coloured = Runs(page).Where(r => r.Look.Bold).Select(r => r.Text.Trim()).ToList();
        Assert.Contains(coloured, t => t.Contains("پایین", StringComparison.Ordinal));
        Assert.Equal("#C2410C", Runs(page).First(r => r.Look.Bold).Look.Color);
    }

    [Fact]
    public void Right_to_left_a_Farsi_squawk_is_a_shout_and_a_triplet_and_the_joiner_stays_in_the_word()
    {
        var tokens = StoryTokens.Split("می‌دید خوب");

        Assert.Equal(["می‌دید", " ", "خوب"], tokens.Select(t => t.Text).ToArray());
        Assert.Single(StoryTriplets.Find(StoryTokens.Split(FarsiSquawk)));
        Assert.Equal(FarsiSquawk, Style(FarsiSquawk, "fa").Text);
    }

    [Fact]
    public void Right_to_left_an_Arabic_squawk_shout_and_triplet_do_not_break_the_text()
    {
        var page = Style(ArabicSquawk, "ar");

        Assert.Equal(ArabicSquawk, page.Text);
        Assert.Equal(3, Runs(page).Count(r => r.Look.Bold && r.Look.Color != "#2B2540"));
    }
}
