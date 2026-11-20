using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests.Kdp;

/// <summary>Slice 37 B: the crop agent's text box (fractions of the trim panel to inches), its colours, and their place in <c>kdp.json</c>.</summary>
public class KdpCoverTextTests
{
    private static readonly RectD Trim = new(0.125, 0.125, 8.5, 8.5);
    private static readonly RectD Barcode = new(Trim.X + 8.5 - 0.375 - 2, Trim.Y + 8.5 - 0.375 - 1.2, 2, 1.2);

    [Fact]
    public void A_box_in_the_middle_becomes_inches_on_the_panel()
    {
        var box = KdpTextBoxMaths.Resolve(new KdpTextBox(0.2, 0.3, 0.6, 0.2), Trim, null);

        Assert.NotNull(box);
        Assert.Equal(Trim.X + 0.2 * 8.5, box.Value.X, 6);
        Assert.Equal(Trim.Y + 0.3 * 8.5, box.Value.Y, 6);
        Assert.Equal(0.6 * 8.5, box.Value.Width, 6);
        Assert.Equal(0.2 * 8.5, box.Value.Height, 6);
    }

    [Fact]
    public void Fractions_are_measured_against_the_whole_panel_picture_including_bleed_when_it_is_given()
    {
        var panel = new RectD(0, 0, 8.625, 8.75);
        var box = KdpTextBoxMaths.Resolve(new KdpTextBox(0.2, 0.3, 0.6, 0.2), Trim, null, null, panel)!.Value;

        Assert.Equal(0.2 * 8.625, box.X, 6);
        Assert.Equal(0.3 * 8.75, box.Y, 6);
        Assert.Equal(0.6 * 8.625, box.Width, 6);
        Assert.Equal(0.2 * 8.75, box.Height, 6);
    }

    [Fact]
    public void A_box_that_reaches_the_edge_is_kept_inside_the_safe_margin()
    {
        var box = KdpTextBoxMaths.Resolve(new KdpTextBox(0, 0, 1, 0.5), Trim, null)!.Value;

        Assert.Equal(Trim.X + KdpTextBoxMaths.SafeMarginInches, box.X, 6);
        Assert.Equal(Trim.Y + KdpTextBoxMaths.SafeMarginInches, box.Y, 6);
        Assert.Equal(8.5 - 2 * KdpTextBoxMaths.SafeMarginInches, box.Width, 6);
        Assert.Equal(Trim.Y + 0.5 * 8.5, box.Y + box.Height, 6);
    }

    [Fact]
    public void A_box_over_the_barcode_is_cut_to_the_part_above_it_when_that_is_larger()
    {
        var box = KdpTextBoxMaths.Resolve(new KdpTextBox(0.1, 0.5, 0.8, 0.5), Trim, Barcode)!.Value;

        Assert.True(box.Y + box.Height <= Barcode.Y - KdpTextBoxMaths.BarcodeClearInches + 1e-9 || box.X + box.Width <= Barcode.X - KdpTextBoxMaths.BarcodeClearInches + 1e-9);
        Assert.False(Overlaps(box, Barcode));
    }

    [Fact]
    public void A_box_beside_the_barcode_keeps_its_height_and_loses_the_width_over_it()
    {
        var wide = new KdpTextBox(0.06, 0.62, 0.88, 0.3);

        var box = KdpTextBoxMaths.Resolve(wide, Trim, Barcode)!.Value;

        Assert.False(Overlaps(box, Barcode));
        Assert.True(box.X + box.Width <= Barcode.X - KdpTextBoxMaths.BarcodeClearInches + 1e-9);
        Assert.Equal(0.3 * 8.5, box.Height, 6);
    }

    [Fact]
    public void A_box_with_no_room_left_after_the_cuts_is_refused()
    {
        var onlyBarcode = new KdpTextBox(0.7, 0.8, 0.25, 0.15);

        Assert.Null(KdpTextBoxMaths.Resolve(onlyBarcode, Trim, Barcode));
        Assert.Null(KdpTextBoxMaths.Resolve(new KdpTextBox(0, 0, 0.05, 0.05), Trim, null));
    }

    [Theory]
    [InlineData(-0.1, 0.1, 0.5, 0.5)]
    [InlineData(0.5, 0.5, 0.6, 0.2)]
    [InlineData(0.1, 0.1, 0, 0.2)]
    [InlineData(0.1, 0.1, 0.5, 1.2)]
    [InlineData(double.NaN, 0.1, 0.5, 0.5)]
    public void A_box_outside_the_panel_or_with_no_size_is_not_valid_and_does_not_resolve(double left, double top, double width, double height)
    {
        var box = new KdpTextBox(left, top, width, height);

        Assert.False(box.IsValid);
        Assert.Null(KdpTextBoxMaths.Resolve(box, Trim, null));
    }

    [Theory]
    [InlineData("#ffffff", "#FFFFFF")]
    [InlineData("1b172e", "#1B172E")]
    [InlineData("#0a0B0c", "#0A0B0C")]
    public void A_colour_is_read_with_or_without_the_hash_and_kept_in_upper_case(string text, string expected)
    {
        Assert.True(KdpHexColor.TryNormalize(text, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#FFF")]
    [InlineData("##FFFFFF")]
    [InlineData("#GGGGGG")]
    [InlineData("#FFFFFFFF")]
    [InlineData("white")]
    public void Anything_but_six_hex_digits_is_not_a_colour(string text)
    {
        Assert.False(KdpHexColor.TryNormalize(text, out _));
    }

    [Fact]
    public void A_text_choice_and_the_dino_colours_are_saved_and_read_back()
    {
        var text = new KdpCoverText(new KdpTextBox(0.1, 0.05, 0.7, 0.2), "#ffffff", "#102030");
        var settings = KdpBookSettings.Empty
            .WithCoverOffset(KdpPictureKeys.Back, 582)
            .WithCoverText(KdpPictureKeys.Back, text)
            .WithCoverText(KdpPictureKeys.Front, text)
            .WithDinoColors(new KdpTextColors("#222222", "#EEEEEE"));

        var read = KdpBookSettings.Parse(settings.ToJson());

        Assert.Equal(582, read.CoverOffsetFor(KdpPictureKeys.Back));
        Assert.Equal(new KdpCoverText(new KdpTextBox(0.1, 0.05, 0.7, 0.2), "#FFFFFF", "#102030"), read.CoverText(KdpPictureKeys.Back));
        Assert.Equal("#102030", read.CoverText(KdpPictureKeys.Front)!.Shadow);
        Assert.Equal(new KdpTextColors("#222222", "#EEEEEE"), read.DinoColors);
    }

    [Fact]
    public void A_file_from_slice_36_with_only_offsets_still_reads_and_has_no_text_choice()
    {
        var read = KdpBookSettings.Parse("""{"covers":{"front":{"cropOffsetPx":146},"back":{"cropOffsetPx":582}}}""");

        Assert.Equal(146, read.CoverOffsetFor(KdpPictureKeys.Front));
        Assert.Null(read.CoverText(KdpPictureKeys.Front));
        Assert.Null(read.DinoColors);
    }

    [Fact]
    public void A_cover_with_only_text_has_no_offset_choice_and_clearing_the_offset_keeps_the_text()
    {
        var text = new KdpCoverText(new KdpTextBox(0.1, 0.05, 0.7, 0.2), "#FFFFFF", "#000000");
        var settings = KdpBookSettings.Empty.WithCoverText(KdpPictureKeys.Front, text);

        Assert.False(settings.HasCoverOffset(KdpPictureKeys.Front));
        Assert.Contains(KdpPictureKeys.Front, settings.Covers);
        var cleared = settings.WithCoverOffset(KdpPictureKeys.Front, 40).WithoutCover(KdpPictureKeys.Front);
        Assert.Equal(text, cleared.CoverText(KdpPictureKeys.Front));
        Assert.False(cleared.HasCoverOffset(KdpPictureKeys.Front));
    }

    [Fact]
    public void A_back_box_that_reaches_the_bottom_is_cut_above_the_qr_strip()
    {
        var strip = KdpTextBoxMaths.QrStrip(Trim);

        var box = KdpTextBoxMaths.Resolve(new KdpTextBox(0.1, 0.4, 0.4, 0.55), Trim, Barcode, strip)!.Value;

        Assert.False(Overlaps(box, strip));
        Assert.True(box.Y + box.Height <= strip.Y + 1e-9);
        Assert.Equal(Trim.Y + Trim.Height - 0.5 - 0.8 - 0.25, strip.Y, 6);
    }

    [Fact]
    public void A_box_that_loses_more_than_half_its_height_to_the_qr_strip_is_unusable_not_a_thin_strip()
    {
        var strip = KdpTextBoxMaths.QrStrip(Trim);

        // Only about 0.58 in is left above the strip: over the 0.3 in minimum, but under half of the 1.9 in the box had.
        Assert.Null(KdpTextBoxMaths.Resolve(new KdpTextBox(0.1, 0.75, 0.5, 0.25), Trim, null, strip));
    }

    [Fact]
    public void An_empty_back_cover_entry_is_not_kept()
    {
        var read = KdpBookSettings.Parse("""{"covers":{"back":{}}}""");

        Assert.Empty(read.Covers);
    }

    [Fact]
    public void A_box_wholly_inside_the_qr_strip_is_unusable()
    {
        Assert.Null(KdpTextBoxMaths.Resolve(new KdpTextBox(0.1, 0.9, 0.5, 0.08), Trim, null, KdpTextBoxMaths.QrStrip(Trim)));
    }

    [Fact]
    public void The_written_kdp_json_holds_only_the_saved_fields_and_reads_back_equal()
    {
        var text = new KdpCoverText(new KdpTextBox(0.1, 0.05, 0.7, 0.2), "#FFFFFF", "#000000");
        var settings = KdpBookSettings.Empty.WithCoverText(KdpPictureKeys.Front, text);

        var json = settings.ToJson();

        Assert.DoesNotContain("Colors", json, StringComparison.Ordinal);
        Assert.DoesNotContain("IsValid", json, StringComparison.Ordinal);
        Assert.Equal(text, KdpBookSettings.Parse(json).CoverText(KdpPictureKeys.Front));
    }

    [Fact]
    public void A_text_choice_is_usable_while_the_offset_matches_the_one_it_was_made_for()
    {
        var text = new KdpCoverText(new KdpTextBox(0.1, 0.05, 0.7, 0.2), "#FFFFFF", "#000000", ForOffsetPx: 300);
        var settings = KdpBookSettings.Empty.WithCoverOffset(KdpPictureKeys.Back, 300).WithCoverText(KdpPictureKeys.Back, text);

        Assert.False(settings.CoverTextStale(KdpPictureKeys.Back));
        Assert.Equal(text, KdpBookSettings.Parse(settings.ToJson()).UsableCoverText(KdpPictureKeys.Back));
        Assert.Contains("\"forOffsetPx\": 300", settings.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_text_choice_is_stale_and_unused_once_the_offset_differs_or_is_cleared()
    {
        var text = new KdpCoverText(new KdpTextBox(0.1, 0.05, 0.7, 0.2), "#FFFFFF", "#000000", ForOffsetPx: 300);
        var settings = KdpBookSettings.Empty.WithCoverOffset(KdpPictureKeys.Back, 300).WithCoverText(KdpPictureKeys.Back, text);

        var moved = settings.WithCoverOffset(KdpPictureKeys.Back, 120);
        var cleared = settings.WithoutCover(KdpPictureKeys.Back);

        Assert.True(moved.CoverTextStale(KdpPictureKeys.Back));
        Assert.Null(moved.UsableCoverText(KdpPictureKeys.Back));
        Assert.NotNull(moved.CoverText(KdpPictureKeys.Back));
        Assert.True(cleared.CoverTextStale(KdpPictureKeys.Back));
    }

    [Fact]
    public void An_old_text_entry_without_the_field_reads_and_counts_as_made_for_the_saved_offset()
    {
        const string json = """{"covers":{"front":{"cropOffsetPx":90,"text":{"box":{"left":0.1,"top":0.05,"width":0.7,"height":0.2},"color":"#FFFFFF","shadow":"#000000"}}}}""";

        var settings = KdpBookSettings.Parse(json);

        Assert.Null(settings.CoverText(KdpPictureKeys.Front)!.ForOffsetPx);
        Assert.False(settings.CoverTextStale(KdpPictureKeys.Front));
        Assert.NotNull(settings.UsableCoverText(KdpPictureKeys.Front));
        Assert.DoesNotContain("forOffsetPx", settings.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Clearing_the_last_choice_of_a_cover_removes_its_entry()
    {
        var text = new KdpCoverText(new KdpTextBox(0.1, 0.05, 0.7, 0.2), "#FFFFFF", "#000000");

        var settings = KdpBookSettings.Empty.WithCoverText(KdpPictureKeys.Front, text).WithoutCoverText(KdpPictureKeys.Front);

        Assert.Empty(settings.Covers);
    }

    [Theory]
    [InlineData("""{"covers":{"front":{"text":{"box":{"left":0.1,"top":0.1,"width":0.5,"height":0.2},"color":"red","shadow":"#000000"}}}}""")]
    [InlineData("""{"covers":{"front":{"text":{"box":{"left":0.6,"top":0.1,"width":0.5,"height":0.2},"color":"#FFFFFF","shadow":"#000000"}}}}""")]
    [InlineData("""{"covers":{"front":{"text":{"box":null,"color":"#FFFFFF","shadow":"#000000"}}}}""")]
    [InlineData("""{"covers":{"front":{"text":{"color":"#FFFFFF","shadow":"#000000"}}}}""")]
    [InlineData("""{"covers":{"front":{"dino":{"color":"#FFFFFF","shadow":"#000000"}}}}""")]
    [InlineData("""{"covers":{"back":{"dino":{"color":"#FFFFFF","shadow":"nope"}}}}""")]
    public void A_bad_text_choice_in_the_file_is_refused(string json)
    {
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Parse(json));
    }

    [Fact]
    public void A_bad_colour_or_box_is_refused_when_set()
    {
        var bad = new KdpCoverText(new KdpTextBox(0.1, 0.1, 0.5, 0.2), "blue", "#000000");

        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Empty.WithCoverText(KdpPictureKeys.Front, bad));
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Empty.WithCoverText(KdpPictureKeys.Front, bad with { Color = "#FFFFFF", Box = new KdpTextBox(0.5, 0.5, 0.9, 0.9) }));
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Empty.WithDinoColors(new KdpTextColors("#FFFFFF", "x")));
    }

    [Fact]
    public void The_front_trim_sits_inside_the_bleed_on_the_outside_edge()
    {
        var ltr = KdpCoverSize.FrontTrim(24, ReadingDirection.LeftToRight);
        var rtl = KdpCoverSize.FrontTrim(24, ReadingDirection.RightToLeft);

        Assert.Equal(KdpCoverSize.FrontPanel(24, ReadingDirection.LeftToRight).X, ltr.X, 6);
        Assert.Equal(KdpCoverSize.BleedInches, rtl.X, 6);
        Assert.Equal(KdpTrim.TrimInches, ltr.Width, 6);
    }

    private static bool Overlaps(RectD a, RectD b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}
