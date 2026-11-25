using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests.Kdp;

/// <summary>Slice 36 F and 37: the Dino line and the choice of the calmest band for the title.</summary>
public class KdpCoverWordingAndBandTests
{
    [Fact]
    public void The_dino_line_and_the_site_are_the_ones_the_owner_asked_for()
    {
        Assert.Equal("Listen to this story in Dino: Kids Books & Stories. Want to join in? Pick a character and speak their lines yourself!", KdpCoverWording.DinoLine);
        Assert.Equal("https://dinoreader.sirous.uk/", KdpCoverWording.DinoReaderUrl);
    }

    [Fact]
    public void The_front_matter_dino_line_is_the_exact_text_the_back_cover_carried_before_it_was_built_from_parts()
    {
        Assert.Equal("Listen to this story in Dino: Kids Books & Stories. Want to join in? Pick a character and speak their lines yourself!", KdpFrontMatterWording.DinoLine);
    }

    [Fact]
    public void The_cover_and_the_last_interior_page_use_the_same_dino_line()
    {
        Assert.Equal(KdpCoverWording.DinoLine, KdpFrontMatterWording.For("T", null, 2026).AboutLine);
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
