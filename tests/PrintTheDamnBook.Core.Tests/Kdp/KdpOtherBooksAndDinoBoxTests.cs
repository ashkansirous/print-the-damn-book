using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests.Kdp;

/// <summary>Slice 41 A and B: the Dino block's box maths and its place in <c>kdp.json</c>, and the seeded pick of the other books for the last page.</summary>
public class KdpOtherBooksAndDinoBoxTests
{
    private static readonly RectD Trim = new(0.125, 0.125, 8.5, 8.5);
    private static readonly RectD Panel = new(0, 0, 8.625, 8.75);
    private static readonly RectD Barcode = new(Trim.X + 8.5 - 0.375 - 2, Trim.Y + 8.5 - 0.375 - 1.2, 2, 1.2);

    private static IEnumerable<string> Ids(int count) => Enumerable.Range(1, count).Select(i => $"book-{i:00}");

    [Fact]
    public void The_pick_never_holds_this_book()
    {
        var picked = KdpOtherBooks.Pick("book-03", Ids(16), id => id);

        Assert.DoesNotContain("book-03", picked);
        Assert.Equal(15, picked.Count);
    }

    [Fact]
    public void The_page_never_grows_past_fifteen_books_however_many_others_there_are()
    {
        Assert.Equal(15, KdpOtherBooks.Pick("book-01", Ids(20), id => id).Count);
        Assert.Equal(15, KdpOtherBooks.Pick("book-01", Ids(200), id => id).Count);
    }

    [Fact]
    public void With_few_other_books_all_of_them_are_shown()
    {
        Assert.Equal(6, KdpOtherBooks.Pick("book-01", Ids(7), id => id).Count);
        Assert.Empty(KdpOtherBooks.Pick("book-01", Ids(1), id => id));
    }

    [Fact]
    public void The_same_book_id_gives_the_same_books_in_the_same_order_whatever_order_the_files_come_in()
    {
        var forward = KdpOtherBooks.Pick("book-05", Ids(20), id => id);
        var backward = KdpOtherBooks.Pick("book-05", Ids(20).Reverse(), id => id);

        Assert.Equal(forward, backward);
        Assert.Equal(forward, KdpOtherBooks.Pick("book-05", Ids(20), id => id));
    }

    [Fact]
    public void A_different_book_id_shuffles_differently()
    {
        var first = KdpOtherBooks.Pick("book-01", Ids(40), id => id);
        var second = KdpOtherBooks.Pick("book-02", Ids(40), id => id);

        Assert.NotEqual(first.Where(id => id != "book-02"), second.Where(id => id != "book-01"));
    }

    [Fact]
    public void A_book_id_listed_twice_counts_once()
    {
        var picked = KdpOtherBooks.Pick("book-01", ["a", "a", "b", "b", "b"], id => id);

        Assert.Equal(2, picked.Count);
        Assert.Equal(2, picked.Distinct().Count());
    }

    [Fact]
    public void A_full_row_of_tiles_leaves_slack_inside_the_safe_width()
    {
        Assert.True(KdpOtherBooks.FullRowPt <= KdpOtherBooks.SafeWidthPt - KdpOtherBooks.RowSlackPt + 1e-9);
        Assert.Equal(KdpOtherBooks.TilePt, Math.Floor(KdpOtherBooks.TilePt));
    }

    [Fact]
    public void A_dino_block_box_puts_the_qr_code_at_the_left_and_the_line_beside_it()
    {
        var block = new RectD(1, 5, 4, 1.5);

        var (qr, words) = KdpTextBoxMaths.SplitDinoBlock(block, rtl: false);

        Assert.Equal(new RectD(1, 5 + (1.5 - KdpTextBoxMaths.QrInches) / 2, KdpTextBoxMaths.QrInches, KdpTextBoxMaths.QrInches), qr);
        Assert.Equal(1 + KdpTextBoxMaths.QrInches + KdpTextBoxMaths.QrGapInches, words.X, 6);
        Assert.Equal(5, words.X + words.Width, 6);
    }

    [Fact]
    public void A_right_to_left_dino_block_box_puts_the_qr_code_at_the_right_and_the_line_fills_the_rest()
    {
        var block = new RectD(1, 5, 4, 1.5);

        var (qr, words) = KdpTextBoxMaths.SplitDinoBlock(block, rtl: true);

        Assert.Equal(5 - KdpTextBoxMaths.QrInches, qr.X, 6);
        Assert.Equal(1, words.X, 6);
        Assert.Equal(4 - KdpTextBoxMaths.QrInches - KdpTextBoxMaths.QrGapInches, words.Width, 6);
        Assert.Equal(1.5, words.Height, 6);
    }

    [Theory]
    [InlineData(15, new[] { 5, 5, 5 })]
    [InlineData(12, new[] { 5, 5, 2 })]
    [InlineData(5, new[] { 5 })]
    [InlineData(0, new int[0])]
    public void The_tiles_go_five_to_a_row(int tiles, int[] rows)
    {
        Assert.Equal(rows, KdpOtherBooks.RowSizes(tiles));
    }

    [Fact]
    public void A_dino_box_in_the_empty_middle_becomes_inches_on_the_panel_as_it_is()
    {
        var block = KdpTextBoxMaths.ResolveBlock(new KdpTextBox(0.1, 0.45, 0.6, 0.15), Trim, Barcode, Panel);

        Assert.NotNull(block);
        Assert.Equal(0.1 * 8.625, block.Value.X, 6);
        Assert.Equal(0.45 * 8.75, block.Value.Y, 6);
        Assert.Equal(0.6 * 8.625, block.Value.Width, 6);
        Assert.Equal(0.15 * 8.75, block.Value.Height, 6);
    }

    [Theory]
    [InlineData(0.1, 0.45, 0.2, 0.15)]
    [InlineData(0.1, 0.45, 0.6, 0.05)]
    [InlineData(0.01, 0.45, 0.6, 0.15)]
    [InlineData(0.1, 0.01, 0.6, 0.15)]
    [InlineData(0.4, 0.45, 0.59, 0.15)]
    [InlineData(0.1, 0.9, 0.6, 0.1)]
    [InlineData(0.5, 0.76, 0.3, 0.1)]
    public void A_dino_box_that_is_too_small_outside_the_safe_margin_or_on_the_barcode_is_refused(double left, double top, double width, double height)
    {
        Assert.Null(KdpTextBoxMaths.ResolveBlock(new KdpTextBox(left, top, width, height), Trim, Barcode, Panel));
    }

    [Fact]
    public void A_dino_box_that_is_not_a_valid_box_is_refused()
    {
        Assert.Null(KdpTextBoxMaths.ResolveBlock(new KdpTextBox(0.6, 0.45, 0.6, 0.15), Trim, Barcode, Panel));
    }

    [Fact]
    public void Boxes_that_share_area_overlap_and_boxes_that_only_touch_do_not()
    {
        Assert.True(KdpTextBoxMaths.Overlaps(new RectD(0, 0, 2, 2), new RectD(1, 1, 2, 2)));
        Assert.False(KdpTextBoxMaths.Overlaps(new RectD(0, 0, 2, 2), new RectD(2, 0, 2, 2)));
    }

    [Fact]
    public void A_dino_box_is_saved_with_its_colours_and_the_crop_offset_it_was_decided_for_and_read_back()
    {
        var settings = KdpBookSettings.Empty
            .WithCoverOffset(KdpPictureKeys.Back, 524)
            .WithDinoText(new KdpTextColors("#ffffff", "#1a2440"), new KdpTextBox(0.1, 0.45, 0.6, 0.15), 524);

        var read = KdpBookSettings.Parse(settings.ToJson());

        Assert.Equal(new KdpDinoText("#FFFFFF", "#1A2440", new KdpTextBox(0.1, 0.45, 0.6, 0.15), 524), read.DinoText);
        Assert.Equal(new KdpTextColors("#FFFFFF", "#1A2440"), read.DinoColors);
        Assert.False(read.DinoBoxStale);
        Assert.NotNull(read.UsableDinoText!.Box);
    }

    [Fact]
    public void A_dino_box_made_for_another_offset_is_stale_and_drops_its_box_but_keeps_its_colours()
    {
        var settings = KdpBookSettings.Empty
            .WithCoverOffset(KdpPictureKeys.Back, 600)
            .WithDinoText(new KdpTextColors("#FFFFFF", "#000000"), new KdpTextBox(0.1, 0.45, 0.6, 0.15), 524);

        Assert.True(settings.DinoBoxStale);
        Assert.Null(settings.UsableDinoText!.Box);
        Assert.Equal("#FFFFFF", settings.UsableDinoText.Color);
    }

    [Fact]
    public void A_dino_box_without_for_offset_counts_as_made_for_the_saved_offset_like_a_cover_text_box()
    {
        var read = KdpBookSettings.Parse("""{"covers":{"back":{"cropOffsetPx":582,"text":{"box":{"left":0.1,"top":0.45,"width":0.6,"height":0.15},"color":"#FFFFFF","shadow":"#000000"},"dino":{"color":"#1F2A44","shadow":"#FFFFFF","box":{"left":0.1,"top":0.45,"width":0.6,"height":0.15}}}}}""");

        Assert.False(read.CoverTextStale(KdpPictureKeys.Back));
        Assert.False(read.DinoBoxStale);
        Assert.NotNull(read.UsableDinoText!.Box);
    }

    [Fact]
    public void A_file_from_slice_37_with_dino_colours_only_still_reads_and_has_no_box()
    {
        var read = KdpBookSettings.Parse("""{"covers":{"back":{"cropOffsetPx":582,"dino":{"color":"#1F2A44","shadow":"#FFFFFF"}}}}""");

        Assert.Equal(new KdpTextColors("#1F2A44", "#FFFFFF"), read.DinoColors);
        Assert.Null(read.DinoText!.Box);
        Assert.False(read.DinoBoxStale);
    }

    [Fact]
    public void Setting_only_the_colours_keeps_a_saved_box_and_clearing_removes_everything()
    {
        var withBox = KdpBookSettings.Empty.WithDinoText(new KdpTextColors("#FFFFFF", "#000000"), new KdpTextBox(0.1, 0.45, 0.6, 0.15), 0);

        var recoloured = withBox.WithDinoColors(new KdpTextColors("#111111", "#EEEEEE"));

        Assert.Equal("#111111", recoloured.DinoText!.Color);
        Assert.Equal(new KdpTextBox(0.1, 0.45, 0.6, 0.15), recoloured.DinoText.Box);
        Assert.Null(withBox.WithoutDinoColors().DinoText);
    }

    [Fact]
    public void A_dino_box_outside_the_panel_is_refused_when_saving_and_when_reading()
    {
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Empty.WithDinoText(new KdpTextColors("#FFFFFF", "#000000"), new KdpTextBox(0.6, 0.45, 0.6, 0.15), 0));
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Parse("""{"covers":{"back":{"dino":{"color":"#FFFFFF","shadow":"#000000","box":{"left":0.7,"top":0.5,"width":0.6,"height":0.1}}}}}"""));
    }

    [Fact]
    public void The_corner_block_puts_the_qr_code_away_from_the_barcode_and_the_line_up_to_the_barcode_clearance()
    {
        var (block, qr, words) = KdpTextBoxMaths.CornerDinoBlock(Trim, Barcode, rtl: false);

        Assert.Equal(Trim.X + KdpTextBoxMaths.PanelInsetInches, qr.X, 6);
        Assert.Equal(Trim.Y + Trim.Height - KdpTextBoxMaths.PanelInsetInches - KdpTextBoxMaths.QrInches, qr.Y, 6);
        Assert.Equal(qr.X + qr.Width + KdpTextBoxMaths.QrGapInches, words.X, 6);
        Assert.Equal(Barcode.X - KdpTextBoxMaths.BarcodeClearInches, words.X + words.Width, 6);
        Assert.Equal(qr.X, block.X, 6);
        Assert.Equal(words.X + words.Width, block.X + block.Width, 6);
    }

    [Fact]
    public void The_corner_block_of_a_right_to_left_book_mirrors_it()
    {
        var barcode = new RectD(Trim.X + 0.375, Barcode.Y, 2, 1.2);
        var (block, qr, words) = KdpTextBoxMaths.CornerDinoBlock(Trim, barcode, rtl: true);

        Assert.Equal(Trim.X + Trim.Width - KdpTextBoxMaths.PanelInsetInches, qr.X + qr.Width, 6);
        Assert.Equal(barcode.X + barcode.Width + KdpTextBoxMaths.BarcodeClearInches, words.X, 6);
        Assert.Equal(qr.X - KdpTextBoxMaths.QrGapInches, words.X + words.Width, 6);
        Assert.Equal(words.X, block.X, 6);
    }

    [Fact]
    public void A_chosen_block_stays_only_when_it_meets_no_credit_lines()
    {
        var block = new RectD(1, 4, 3, 0.8);

        Assert.True(KdpTextBoxMaths.ChosenBlockClearOfCredits(block, null));
        Assert.True(KdpTextBoxMaths.ChosenBlockClearOfCredits(block, new RectD(1, 1, 3, 1)));
        Assert.False(KdpTextBoxMaths.ChosenBlockClearOfCredits(block, new RectD(1, 4.5, 3, 1)));
    }

    [Fact]
    public void The_heading_names_the_author_in_one_place()
    {
        var matter = KdpFrontMatterWording.For("A Title", null, 2026);

        Assert.Equal("More books by Ashkan Sirous", matter.OtherBooksHeading);
    }
}
