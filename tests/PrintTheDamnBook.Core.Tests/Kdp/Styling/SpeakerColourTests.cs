using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Text;
using static PrintTheDamnBook.Core.Tests.Kdp.Styling.StyleTestSupport;

namespace PrintTheDamnBook.Core.Tests.Kdp.Styling;

/// <summary>Slice 46 item E: the speaker colour rules and the fallback palette (<c>docs/design/kdp-typography.md</c>).</summary>
public class SpeakerColourTests
{
    private static readonly Rgb PalePink = new(0xFB, 0xE8, 0xEF);
    private static readonly IReadOnlyList<Rgb> Tints = [PalePink, new(0xE8, 0xF3, 0xFB), new(0xFB, 0xF5, 0xE0)];

    private static SpeakerInfo Speaker(string name, int lines) => new(name, lines, [1]);

    [Fact]
    public void A_dark_green_passes_every_rule()
    {
        Assert.Null(SpeakerColourRules.Check("#2F6B34", Tints, []));
    }

    [Fact]
    public void The_first_fallback_blue_is_close_to_the_blue_triplet_colour_so_a_saved_choice_may_not_use_it_but_the_fallback_does()
    {
        Assert.Contains("too close", SpeakerColourRules.Check("#1F4FA0", Tints, []), StringComparison.Ordinal);
        Assert.Null(SpeakerColourRules.Check("#1F4FA0", Tints, [], againstTriplets: false));
    }

    [Fact]
    public void A_colour_too_light_for_white_is_refused_with_the_contrast()
    {
        var why = SpeakerColourRules.Check("#9CB4E0", [], []);

        Assert.Contains("too light", why, StringComparison.Ordinal);
        Assert.Contains("white", why, StringComparison.Ordinal);
    }

    [Fact]
    public void A_colour_that_reads_on_white_but_not_on_a_tint_is_refused_naming_the_tint()
    {
        var darkTint = new Rgb(0x6A, 0x78, 0x90);

        var why = SpeakerColourRules.Check("#5B6BA0", [darkTint], []);

        Assert.Contains("#6A7890", why, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("#8B1A1A")]
    [InlineData("#A31515")]
    [InlineData("#9A1B4B")]
    public void A_red_is_refused(string hex)
    {
        Assert.Contains("red", SpeakerColourRules.Check(hex, [], []), StringComparison.Ordinal);
    }

    [Fact]
    public void A_dark_grey_with_no_chroma_is_not_red()
    {
        Assert.Null(SpeakerColourRules.Check("#4A4A4A", [], []));
    }

    [Fact]
    public void A_colour_close_to_another_speaker_or_to_a_triplet_colour_is_refused()
    {
        Assert.Contains("too close", SpeakerColourRules.Check("#1F4FA2", [], ["#1F4FA0"]), StringComparison.Ordinal);
        Assert.Contains("too close", SpeakerColourRules.Check("#1F5AA8", [], []), StringComparison.Ordinal);
    }

    [Fact]
    public void A_text_that_is_not_a_colour_is_refused()
    {
        Assert.Contains("not a colour", SpeakerColourRules.Check("blue", [], []), StringComparison.Ordinal);
    }

    [Fact]
    public void The_fallback_palette_gives_the_first_five_speakers_the_five_colours_in_order()
    {
        var speakers = new[] { Speaker("Seagull", 9), Speaker("Kathryn", 8), Speaker("Izzy", 4), Speaker("Billy", 3), Speaker("Pancake Polly", 2) };

        var result = SpeakerPalette.Resolve(speakers, new Dictionary<string, string>(), Tints);

        Assert.Equal(["#1F4FA0", "#0E6B78", "#6A3A96", "#2F6B34", "#8A4B12"], speakers.Select(s => result.Colors[s.Name]).ToList());
        Assert.Equal(5, result.FallbackNames.Count);
        Assert.Empty(result.Rejected);
    }

    [Fact]
    public void Every_fallback_colour_reads_on_white_and_is_not_red()
    {
        foreach (var hex in SpeakerPalette.Fallback)
        {
            Assert.Null(SpeakerColourRules.Check(hex, Tints, [], againstTriplets: false));
        }
    }

    [Fact]
    public void A_saved_colour_is_used_and_the_others_take_the_fallback()
    {
        var speakers = new[] { Speaker("Seagull", 9), Speaker("Kathryn", 8) };

        var result = SpeakerPalette.Resolve(speakers, new Dictionary<string, string> { ["Kathryn"] = "#2F6B34" }, Tints);

        Assert.Equal("#2F6B34", result.Colors["Kathryn"]);
        Assert.Equal("#1F4FA0", result.Colors["Seagull"]);
        Assert.Equal(["Seagull"], result.FallbackNames);
    }

    [Fact]
    public void A_saved_colour_that_fails_a_rule_is_refused_with_the_reason_and_the_fallback_is_used()
    {
        var result = SpeakerPalette.Resolve([Speaker("Seagull", 9)], new Dictionary<string, string> { ["Seagull"] = "#9CB4E0" }, Tints);

        Assert.Contains("too light", result.Rejected["Seagull"], StringComparison.Ordinal);
        Assert.Equal("#1F4FA0", result.Colors["Seagull"]);
        Assert.Contains("Seagull", result.FallbackNames);
    }

    [Fact]
    public void Two_saved_colours_that_are_too_alike_keep_the_first_and_refuse_the_second()
    {
        var speakers = new[] { Speaker("Seagull", 9), Speaker("Kathryn", 8) };

        var result = SpeakerPalette.Resolve(speakers, new Dictionary<string, string> { ["Seagull"] = "#2F6B34", ["Kathryn"] = "#2F6B36" }, Tints);

        Assert.Equal("#2F6B34", result.Colors["Seagull"]);
        Assert.Contains("too close", result.Rejected["Kathryn"], StringComparison.Ordinal);
        Assert.NotEqual(result.Colors["Seagull"], result.Colors["Kathryn"]);
    }

    [Fact]
    public void A_palette_that_runs_out_still_gives_every_speaker_a_different_colour_that_reads()
    {
        var speakers = Enumerable.Range(1, 12).Select(i => Speaker($"Animal {i}", 20 - i)).ToList();

        var result = SpeakerPalette.Resolve(speakers, new Dictionary<string, string>(), Tints);

        Assert.Equal(12, result.Colors.Values.Distinct().Count());
        Assert.All(result.Colors.Values, hex => Assert.True(StyleColors.Contrast(StyleColors.Parse(hex), PalePink) >= 4.5, hex));
    }

    [Fact]
    public void The_speaker_colours_of_the_catalog_fallback_are_clearly_different_from_each_other()
    {
        var firstFive = SpeakerPalette.Fallback.Take(5).Select(StyleColors.Parse).ToList();

        for (var a = 0; a < firstFive.Count; a++)
        {
            for (var b = a + 1; b < firstFive.Count; b++)
            {
                Assert.True(SpeakerColourRules.Distance(firstFive[a], firstFive[b]) >= SpeakerColourRules.MinDistance, $"{a} and {b}");
            }
        }
    }
}
