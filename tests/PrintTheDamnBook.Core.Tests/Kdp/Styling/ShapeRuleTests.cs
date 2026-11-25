using PrintTheDamnBook.Core.Kdp.Styling;
using static PrintTheDamnBook.Core.Tests.Kdp.Styling.StyleTestSupport;

namespace PrintTheDamnBook.Core.Tests.Kdp.Styling;

/// <summary>Slice 46 item F: R6 shapes. A triplet whose word is in a meaning list is moved, sized or turned by its class; every class has a test, with excerpts of the catalog books.</summary>
public class ShapeRuleTests
{
    private const string Cow = "The cow went moo, moo, moo in the field.";
    private const string Rumble = "His tummy went rumble, rumble, rumble!";
    private const string Splash = "Splish, splash, splosh! Behind her, the pirates came through the water.";

    private static ShapeClass Class(string text)
    {
        var tokens = StoryTokens.Split(text);
        var triplet = StoryTriplets.Find(tokens).Single();
        return StoryShapes.Classify(StoryLexicon.English, tokens, triplet);
    }

    /// <summary>The looks of the three words of the page's triplet, first to last, by finding each in the page's runs.</summary>
    private static List<RunLook> ThreeWords(StyledPage page, string word)
    {
        return Runs(page).Where(r => r.Text.Contains(word, StringComparison.OrdinalIgnoreCase)).Select(r => r.Look).Take(3).ToList();
    }

    [Fact]
    public void Each_class_is_found_from_its_meaning_list()
    {
        Assert.Equal(ShapeClass.Down, Class("Down, down, down swooped Seagull."));
        Assert.Equal(ShapeClass.Up, Class("Up, up, up went the kite."));
        Assert.Equal(ShapeClass.Loud, Class(Cow));
        Assert.Equal(ShapeClass.Shake, Class(Rumble));
        Assert.Equal(ShapeClass.Bounce, Class(Splash));
    }

    [Fact]
    public void A_sound_with_a_quiet_word_in_its_sentence_or_attribution_is_quiet()
    {
        Assert.Equal(ShapeClass.Quiet, Class(XMarksSoftly));
        Assert.Equal(ShapeClass.Quiet, Class("The cow went moo, moo, moo, very softly."));
        Assert.Equal(ShapeClass.Loud, Class("\"Moo, moo, moo!\" said the cow. She spoke softly after."));
    }

    [Fact]
    public void A_triplet_that_means_nothing_has_no_shape()
    {
        Assert.Equal(ShapeClass.None, Class(XMarksLong));
        Assert.Equal(ShapeClass.None, Class("Dig, dig, dig! The pirates dug."));
    }

    [Fact]
    public void Downward_words_sit_0_then_3_then_6_pt_lower_and_keep_the_triplet_colours_and_bold()
    {
        var page = Style(XMarksSquawk);

        var looks = ThreeWords(page, "down");
        Assert.Equal([0.0, -3.0, -6.0], looks.Select(l => l.BaselinePt));
        Assert.Equal(StyleSpec.TripletColors, looks.Select(l => l.Color));
        Assert.All(looks, l => Assert.True(l.Bold));
        Assert.Equal(24, looks[2].Pt);
    }

    [Fact]
    public void Upward_words_sit_0_then_3_then_6_pt_higher()
    {
        var looks = ThreeWords(Style("Up, up, up went the kite."), "up");

        Assert.Equal([0.0, 3.0, 6.0], looks.Select(l => l.BaselinePt));
    }

    [Fact]
    public void Bouncing_words_sit_3_up_2_down_2_up()
    {
        var page = Style(Splash);

        Assert.Equal([3.0, -2.0, 2.0], new[] { "Splish", "splash", "splosh" }.Select(w => LookOf(page, w).BaselinePt));
    }

    [Fact]
    public void Loud_sound_words_grow_24_27_30_pt()
    {
        var page = Style(Cow);

        Assert.Equal([24.0, 27.0, 30.0], ThreeWords(page, "moo").Select(l => l.Pt));
    }

    [Fact]
    public void Quiet_sound_words_shrink_24_21_19_pt()
    {
        var page = Style(XMarksSoftly);

        var looks = Runs(page).Where(r => r.Text.Contains("quawk", StringComparison.OrdinalIgnoreCase)).Select(r => r.Look).ToList();
        Assert.Equal([24.0, 21.0, 19.0], looks.Select(l => l.Pt));
    }

    [Fact]
    public void Shaking_words_turn_minus_4_plus_3_minus_3_degrees_with_a_2_pt_margin()
    {
        var looks = ThreeWords(Style(Rumble), "rumble");

        Assert.Equal([-4.0, 3.0, -3.0], looks.Select(l => l.RotationDeg));
        Assert.All(looks, l => Assert.Equal(2, l.SideMarginPt));
        Assert.All(looks, l => Assert.True(l.NeedsInlineElement));
    }

    [Fact]
    public void A_move_along_the_baseline_needs_the_triplet_and_its_neighbours_at_24_pt()
    {
        var page = Style("It was a big down, down, down.");

        Assert.All(ThreeWords(page, "down"), l => Assert.Equal(0, l.BaselinePt));
    }

    [Fact]
    public void A_neighbour_in_a_refrain_size_also_keeps_the_move_off()
    {
        var book = new[] { "Hooray for the treasure! Splish, splash, splosh!", "Hooray for the treasure!" };
        var styler = new StoryStyler(book, "en");

        var page = styler.Style(0, null);
        Assert.Equal(StyleSpec.BigPt, LookOf(page, "treasure").Pt);
        Assert.Equal(0, LookOf(page, "Splish").BaselinePt);
        Assert.Equal(0, LookOf(page, "splosh").BaselinePt);
    }

    [Fact]
    public void Size_and_turn_shapes_do_not_need_24_pt_neighbours()
    {
        var page = Style("It was a big moo, moo, moo.");

        Assert.Equal([24.0, 27.0, 30.0], ThreeWords(page, "moo").Select(l => l.Pt));
    }

    [Fact]
    public void A_shape_moves_nothing_in_a_language_without_a_list_and_changes_no_word()
    {
        var page = Style(FarsiTriplet, "fa");

        Assert.All(Runs(page), r => Assert.Equal(0, r.Look.BaselinePt));
        Assert.Equal(FarsiTriplet, page.Text);
    }

    [Fact]
    public void Shapes_never_change_a_word()
    {
        foreach (var text in new[] { XMarksSquawk, Splash, Rumble, Cow, XMarksSoftly, DuckSplash })
        {
            Assert.Equal(text.Replace("\n\n", "\n", StringComparison.Ordinal), Style(text).Text);
        }
    }

    [Fact]
    public void The_padding_of_a_turned_box_is_half_the_height_it_gains()
    {
        var padding = ShapeMaths.RotationPaddingPt(90, 32.4, 4);

        Assert.Equal((90 * Math.Sin(4 * Math.PI / 180) + 32.4 * Math.Cos(4 * Math.PI / 180) - 32.4) / 2, padding, 6);
        Assert.InRange(padding, 3, 3.3);
        Assert.Equal(0, ShapeMaths.RotationPaddingPt(90, 32.4, 0));
        Assert.Equal(ShapeMaths.RotationPaddingPt(90, 32.4, 4), ShapeMaths.RotationPaddingPt(90, 32.4, -4));
    }

    [Fact]
    public void A_longer_word_needs_more_room_when_turned()
    {
        var short1 = ShapeMaths.PaddingFor(new RunLook(24, Bold: true, RotationDeg: 4), "ab", 1.35);
        var long1 = ShapeMaths.PaddingFor(new RunLook(24, Bold: true, RotationDeg: 4), "wobble,", 1.35);

        Assert.True(long1 > short1);
    }
}
