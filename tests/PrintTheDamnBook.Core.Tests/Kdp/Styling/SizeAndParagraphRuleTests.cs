using PrintTheDamnBook.Core.Kdp.Styling;
using static PrintTheDamnBook.Core.Tests.Kdp.Styling.StyleTestSupport;

namespace PrintTheDamnBook.Core.Tests.Kdp.Styling;

public class SizeAndParagraphRuleTests
{
    [Fact]
    public void R1_a_newline_in_the_source_starts_a_paragraph_and_the_gap_is_14_pt()
    {
        var page = Style(XMarksPage1);

        Assert.Equal(2, page.Paragraphs.Count);
        Assert.StartsWith("Seagull flew high", page.Paragraphs[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("Then she spotted", page.Paragraphs[1].Text, StringComparison.Ordinal);
        Assert.Equal(14, StyleSpec.ParagraphGapPt);
    }

    [Fact]
    public void R1_holds_in_every_language_and_a_single_paragraph_stays_one()
    {
        Assert.Equal(2, Style(XMarksPage1, "fr").Paragraphs.Count);
        Assert.Single(Style(DuckTinyBit).Paragraphs);
    }

    [Theory]
    [InlineData(XMarksPage1)]
    [InlineData(XMarksSquawk)]
    [InlineData(GingerRun)]
    [InlineData(PigsKnock)]
    [InlineData(RedBigEyes)]
    [InlineData(DuckGreatBig)]
    [InlineData(FarsiTriplet)]
    public void Styling_never_changes_the_words_or_the_spaces(string text)
    {
        Assert.Equal(PrintTheDamnBook.Core.Text.StoryLines.Clean(text), Style(text).Text);
    }

    [Fact]
    public void R2_big_words_are_30_pt_bold_and_great_big_is_a_pair()
    {
        var page = Style(DuckGreatBig);

        Assert.Equal(new RunLook(30, Bold: true), LookOf(page, "great"));
        Assert.Equal(30, LookOf(page, "big").Pt);
        Assert.True(LookOf(page, "big").Bold);
        Assert.True(IsPlain(LookOf(page, "neck")));
    }

    [Fact]
    public void R2_great_alone_is_not_big()
    {
        var page = Style("It was a great day.");

        Assert.True(IsPlain(LookOf(page, "great")));
    }

    [Fact]
    public void R2_small_words_are_19_pt_regular()
    {
        var page = Style(DuckTinyBit);

        Assert.Equal(new RunLook(19), LookOf(page, "tiny"));
        Assert.Equal(new RunLook(19), Style(PigsNames).Paragraphs[0].Runs.First(r => r.Text.Contains("little", StringComparison.Ordinal)).Look);
    }

    [Fact]
    public void R2_quiet_words_are_19_pt_italic_in_muted_ink()
    {
        var look = LookOf(Style(XMarksSoftly), "softly");

        Assert.Equal(new RunLook(19, Italic: true, Color: "#6E6880"), look);
    }

    [Fact]
    public void R2_matches_whole_words_in_any_case()
    {
        Assert.Equal(30, LookOf(Style("Huge waves came."), "Huge").Pt);
        Assert.True(IsPlain(LookOf(Style("The biggestt thing."), "biggestt")));
    }

    [Fact]
    public void R9_stretched_words_get_022_em_of_letter_spacing()
    {
        Assert.Equal(0.22, LookOf(Style(PigsSlowly), "slowly").LetterSpacingEm);
        Assert.Equal(0.22, LookOf(Style(XMarksLong), "slow").LetterSpacingEm);
        Assert.Equal(0, LookOf(Style(PigsSlowly), "carefully").LetterSpacingEm);
    }

    [Fact]
    public void A_language_without_word_lists_skips_R2_and_R9_quietly()
    {
        var page = Style(DuckGreatBig + " Pim worked slowly.", "fr");

        Assert.True(IsPlain(LookOf(page, "great")));
        Assert.True(IsPlain(LookOf(page, "slowly")));
        Assert.Null(StoryLexicon.For("fr"));
        Assert.NotNull(StoryLexicon.For("en-GB"));
    }
}
