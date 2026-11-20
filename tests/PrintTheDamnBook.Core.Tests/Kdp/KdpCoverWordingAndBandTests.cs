using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests.Kdp;

/// <summary>Slice 36 F: the cover's credit line and the choice of the calmest band for the title.</summary>
public class KdpCoverWordingAndBandTests
{
    [Fact]
    public void The_front_credit_names_the_writers_and_the_illustrator()
    {
        var credits = new DinoCredits("Elham Shafiei Gol; Ashkan Sirous", false, "Ashkan Sirous", 2026, 10);

        Assert.Equal("Written by Elham Shafiei Gol and Ashkan Sirous · Illustrated by Ashkan Sirous", KdpCoverWording.FrontCredit(credits, "en"));
    }

    [Fact]
    public void Three_writers_are_listed_with_commas_and_a_last_and()
    {
        var credits = new DinoCredits("A; B; C", false, "D", 2026, 10);

        Assert.Equal("Written by A, B and C · Illustrated by D", KdpCoverWording.FrontCredit(credits, "en"));
    }

    [Fact]
    public void A_traditional_tale_has_only_the_illustrator()
    {
        var credits = new DinoCredits("traditional", false, "Ashkan Sirous", 2026, 10);

        Assert.Equal("Illustrated by Ashkan Sirous", KdpCoverWording.FrontCredit(credits, "en"));
    }

    [Fact]
    public void A_retold_book_credits_the_original_story_like_the_back_cover()
    {
        var credits = new DinoCredits("Aesop", true, "Ashkan Sirous", 2026, 10);

        Assert.Equal("Original story by Aesop · Illustrated by Ashkan Sirous", KdpCoverWording.FrontCredit(credits, "en"));
        Assert.Equal(BackCoverWording.Lines(credits, "en")[0] + " · Illustrated by Ashkan Sirous", KdpCoverWording.FrontCredit(credits, "en"));
    }

    [Theory]
    [InlineData(80, KdpCalmBand.NormalShadowAlpha)]
    [InlineData(169, KdpCalmBand.NormalShadowAlpha)]
    [InlineData(170, KdpCalmBand.StrongShadowAlpha)]
    [InlineData(230, KdpCalmBand.StrongShadowAlpha)]
    public void The_shadow_behind_white_letters_is_stronger_on_a_light_band(double brightness, byte expected)
    {
        Assert.Equal(expected, KdpCalmBand.ShadowAlpha(brightness));
    }

    [Fact]
    public void A_book_without_credits_has_no_front_credit()
    {
        Assert.Null(KdpCoverWording.FrontCredit(null, "en"));
    }

    [Fact]
    public void Another_language_uses_its_own_story_by_and_illustration_words()
    {
        var credits = new DinoCredits("Aesop", false, "Ashkan Sirous", 2026, 10);

        var line = KdpCoverWording.FrontCredit(credits, "de");

        Assert.Equal("Geschichte von Aesop · Illustration: Ashkan Sirous", line);
    }

    [Fact]
    public void The_dino_line_and_the_site_are_the_ones_the_owner_asked_for()
    {
        Assert.Equal("Hear this story in Dino: Kids Books & Stories, and speak as the characters yourself!", KdpCoverWording.DinoLine);
        Assert.Equal("https://dinoreader.sirous.uk/", KdpCoverWording.DinoReaderUrl);
    }

    [Fact]
    public void The_candidate_bands_run_from_the_top_margin_to_the_bottom_margin_inside_the_trim()
    {
        var trim = new RectD(0, 0.125, 8.5, 8.5);

        var bands = KdpCalmBand.Candidates(trim, 7.5, 2.3);

        Assert.Equal(4, bands.Count);
        Assert.Equal(0.625, bands[0].Y, 6);
        Assert.Equal(8.625 - 0.5 - 2.3, bands[^1].Y, 6);
        Assert.All(bands, b => Assert.Equal((0.5, 7.5, 2.3), (b.X, b.Width, b.Height)));
    }

    [Fact]
    public void The_calmest_band_is_the_one_with_the_least_spread_in_brightness()
    {
        // 10 x 10 grid for a 10 x 10 in picture: the top half is a busy checkerboard, the bottom half flat grey.
        var pixels = new byte[100];
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                pixels[y * 10 + x] = y < 5 ? (byte)((x + y) % 2 == 0 ? 20 : 230) : (byte)128;
            }
        }

        var bands = new[] { new RectD(1, 0.5, 8, 2), new RectD(1, 6, 8, 2) };

        var chosen = KdpCalmBand.Choose(bands, new LumaImage(10, 10, pixels), (0, 0), (10, 10));

        Assert.Equal(1, chosen);
    }

    [Fact]
    public void Equally_calm_bands_choose_the_higher_one()
    {
        var flat = new LumaImage(4, 4, Enumerable.Repeat((byte)100, 16).ToArray());
        var bands = new[] { new RectD(0, 0, 4, 1), new RectD(0, 2, 4, 1) };

        Assert.Equal(0, KdpCalmBand.Choose(bands, flat, (0, 0), (4, 4)));
    }
}
