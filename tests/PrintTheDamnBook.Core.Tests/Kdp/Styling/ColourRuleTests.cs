using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Text;
using static PrintTheDamnBook.Core.Tests.Kdp.Styling.StyleTestSupport;

namespace PrintTheDamnBook.Core.Tests.Kdp.Styling;

public class ColourRuleTests
{
    private static readonly Rgb PaleTint = new(0xF6, 0xF1, 0xDA);

    [Fact]
    public void R3_a_colour_word_and_the_next_word_are_bold_in_that_colour()
    {
        var page = Style(XMarksSquawk, tint: White);

        var red = LookOf(page, "red");
        Assert.True(red.Bold);
        Assert.Equal(red, LookOf(page, "X"));
        Assert.Equal("#C62828", red.Color);
    }

    [Fact]
    public void R3_red_hood_colours_red_and_the_word_after_it_only()
    {
        var page = Style(RedHood, tint: White);

        Assert.Equal(LookOf(page, "green"), LookOf(page, "wood"));
        Assert.Equal(LookOf(page, "red"), LookOf(page, "hood"));
        Assert.NotEqual(LookOf(page, "green").Color, LookOf(page, "red").Color);
        Assert.True(IsPlain(LookOf(page, "bright")));
    }

    [Fact]
    public void R3_the_colour_does_not_reach_across_a_full_stop_or_a_quote()
    {
        var page = Style("The hat was red. Then it rained.", tint: White);

        Assert.True(LookOf(page, "red").Bold);
        Assert.True(IsPlain(LookOf(page, "Then")));
    }

    [Fact]
    public void R3_darkens_a_pale_colour_until_it_has_4_point_5_to_1_on_the_page_tint()
    {
        var page = Style(DuckYellow, tint: PaleTint);

        var look = LookOf(page, "yellow");
        Assert.NotEqual("#B8860B", look.Color);
        Assert.True(StyleColors.Contrast(StyleColors.Parse(look.Color), PaleTint) >= 4.5);
        Assert.Equal(look, LookOf(page, "ducklings"));
    }

    [Fact]
    public void R3_leaves_a_colour_that_already_reads_alone()
    {
        Assert.Equal("#C62828", LookOf(Style("A big red X.", tint: White), "red").Color);
    }

    [Fact]
    public void R3_every_colour_in_the_table_reads_on_white_and_on_a_pale_tint()
    {
        foreach (var colour in StoryLexicon.English.Colours.Keys)
        {
            foreach (var paper in new[] { White, PaleTint })
            {
                var look = LookOf(Style($"The {colour} cat sat.", tint: paper), colour);

                Assert.True(StyleColors.Contrast(StyleColors.Parse(look.Color), paper) >= 4.5, $"{colour} on {StyleColors.ToHex(paper)}");
            }
        }
    }

    [Fact]
    public void R3_skips_a_language_without_a_word_list()
    {
        Assert.True(IsPlain(LookOf(Style(XMarksBlueBowl, "de"), "blue")));
    }

    [Fact]
    public void R3_in_a_big_blue_bowl_keeps_the_size_of_the_big_word_and_colours_blue_and_bowl()
    {
        var page = Style(XMarksBlueBowl, tint: White);

        Assert.Equal(30, LookOf(page, "big").Pt);
        Assert.Equal(LookOf(page, "blue"), LookOf(page, "bowl"));
        Assert.Equal(24, LookOf(page, "blue").Pt);
    }

    [Fact]
    public void Contrast_is_21_for_black_on_white_and_1_for_the_same_colour()
    {
        Assert.Equal(21, StyleColors.Contrast(new Rgb(0, 0, 0), White), 3);
        Assert.Equal(1, StyleColors.Contrast(PaleTint, PaleTint), 6);
    }

    [Fact]
    public void Hex_parses_and_prints_back()
    {
        Assert.Equal(new Rgb(0x2B, 0x25, 0x40), StyleColors.Parse("#2B2540"));
        Assert.Equal("#2B2540", StyleColors.ToHex(new Rgb(0x2B, 0x25, 0x40)));
        Assert.Throws<ArgumentException>(() => StyleColors.Parse("#FFF"));
    }

    [Fact]
    public void Darkening_keeps_the_hue_of_the_colour()
    {
        var dark = StyleColors.DarkenToContrast(new Rgb(0xB8, 0x86, 0x0B), White);

        Assert.True(dark.R > dark.B && dark.G > dark.B, "still a yellow-brown");
        Assert.True(dark.R >= dark.G, "red channel leads, as in the base colour");
    }
}
