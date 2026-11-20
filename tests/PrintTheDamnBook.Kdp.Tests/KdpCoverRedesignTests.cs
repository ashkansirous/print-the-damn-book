using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp.Cover;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SkiaSharp;
using UglyToad.PdfPig;
using ZXing;
using ZXing.Common;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>Slice 36 F: words straight on the pictures (no white boxes), the credit lines, the QR code, and the crop offsets of the two cover pictures.</summary>
public class KdpCoverRedesignTests
{
    private static readonly DinoCredits Credits = new("Ann Author", false, "Ashkan Sirous", 2026, 11);

    private static KdpCoverInput Input(KdpBookSettings? settings = null, bool withBack = true) =>
        new("Freddy and the Test", "en", Credits, SyntheticBook.Picture(1), withBack ? SyntheticBook.Picture(2) : null, 24, Settings: settings);

    [Fact]
    public void The_qr_code_on_the_back_cover_decodes_to_the_dino_reader_site()
    {
        var png = KdpQrCode.Png(KdpCoverWording.DinoReaderUrl);

        Assert.Equal(KdpCoverWording.DinoReaderUrl, Decode(SKBitmap.Decode(png)));
    }

    [Fact]
    public void The_drawn_cover_holds_a_qr_code_that_decodes_to_the_dino_reader_site()
    {
        var page = RenderedCover(Input());

        Assert.Equal(KdpCoverWording.DinoReaderUrl, Decode(page));
    }

    [Fact]
    public void The_qr_code_is_about_point_eight_inches_wide_on_the_side_away_from_the_barcode()
    {
        var result = KdpCover.Build(Input());

        using var pdf = PdfDocument.Open(result.Pdf);
        var qr = Qr(pdf.GetPage(1));
        Assert.Equal(0.8, qr.BoundingBox.Width / 72, 0.02);
        var barcodeLeft = result.Barcode.X * 72;
        Assert.True(qr.BoundingBox.Right < barcodeLeft);
    }

    [Fact]
    public void A_right_to_left_cover_puts_the_qr_code_on_the_barcode_free_right_of_its_back()
    {
        var input = Input() with { Language = "fa", Direction = ReadingDirection.RightToLeft };

        var result = KdpCover.Build(input);

        using var pdf = PdfDocument.Open(result.Pdf);
        var qr = Qr(pdf.GetPage(1));
        Assert.True(qr.BoundingBox.Left > (result.Barcode.X + result.Barcode.Width) * 72);
    }

    [Fact]
    public void No_white_box_sits_behind_the_words_only_the_barcode_box_is_white()
    {
        var result = KdpCover.Build(Input());

        using var pdf = PdfDocument.Open(result.Pdf);
        var barcode = result.Barcode;
        var barcodeRect = new { Left = barcode.X * 72, Right = (barcode.X + barcode.Width) * 72 };
        var whites = pdf.GetPage(1).Paths.Where(IsWhiteFill).Select(p => p.GetBoundingRectangle()).Where(r => r is { } box && box.Width < pdf.GetPage(1).Width * 0.9).ToList();
        Assert.All(whites, box => Assert.InRange(box!.Value.Left, barcodeRect.Left - 1, barcodeRect.Right + 1));
    }

    [Fact]
    public void The_front_has_the_title_only_and_the_back_the_credit_lines_and_the_new_dino_line()
    {
        var result = KdpCover.Build(Input());

        using var pdf = PdfDocument.Open(result.Pdf);
        var text = string.Join(' ', pdf.GetPage(1).GetWords().Select(w => w.Text));
        Assert.Contains("Freddy", text);
        Assert.Contains("Author", text);
        Assert.Contains("Illustration:", text);
        Assert.Contains("Listen to this story in Dino", text);
        Assert.Contains("Pick a character and speak their lines yourself!", text);
        Assert.Equal(0, Count(text, "Written by"));
        Assert.Equal(0, Count(text, "Illustrated by"));
    }

    [Fact]
    public void The_text_layer_holds_the_title_the_credits_and_the_dino_line_once_each_not_as_stacked_shadow_copies()
    {
        var result = KdpCover.Build(Input());

        using var pdf = PdfDocument.Open(result.Pdf);
        var text = string.Join(' ', pdf.GetPage(1).GetWords().Select(w => w.Text));
        Assert.Equal(1, Count(text, "Freddy"));
        Assert.Equal(1, Count(text, "Author"));
        Assert.Equal(1, Count(text, "Listen"));
    }

    [Fact]
    public void The_shadow_is_one_transparent_image_per_text_block_beneath_the_text_not_vector_copies()
    {
        var result = KdpCover.Build(Input());

        using var pdf = PdfDocument.Open(result.Pdf);
        var images = pdf.GetPage(1).GetImages().ToList();
        Assert.Equal(2 + 1 + 3, images.Count); // front, back, QR and one soft shadow each for title, credits and Dino line
        Assert.Single(images, IsQr);
    }

    [Fact]
    public void A_soft_shadow_image_is_blurred_with_semi_transparent_edges_in_the_chosen_colour()
    {
        var png = KdpSoftShadow.Png(3, 1, 30, (c, color) => c.AlignMiddle().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(30).FontColor(color));
            t.Span("Soft");
        }), "#FF0000");

        using var bitmap = SKBitmap.Decode(png);
        Assert.InRange(bitmap.Width, (int)Math.Round((3 + 2 * KdpSoftShadow.PadInches) * 300) - 2, (int)Math.Round((3 + 2 * KdpSoftShadow.PadInches) * 300) + 2);
        var partial = 0;
        var solid = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha is > 10 and < 245)
                {
                    partial++;
                    Assert.True(pixel.Red > pixel.Blue + 50 && pixel.Red > pixel.Green + 50, $"shadow pixel ({x},{y}) is not red: {pixel}");
                }
                else if (pixel.Alpha >= 245)
                {
                    solid++;
                }
            }
        }

        Assert.True(partial > 1000, $"only {partial} soft pixels");
        Assert.True(partial > solid, $"the shadow is hard: {partial} soft against {solid} solid pixels");
    }

    [Fact]
    public void The_shadow_ink_sits_where_the_letters_are_in_a_box_taller_than_the_words()
    {
        var middle = InkCentreY(c => c.AlignMiddle());
        var bottom = InkCentreY(c => c.AlignBottom());
        var top = InkCentreY(c => c.AlignTop());

        // Box 1 in tall at 300 dpi plus 0.15 in padding above: the box's middle is at 195 px (the shadow is nudged 3 px down).
        Assert.InRange(middle, 195 - 20, 195 + 20);
        Assert.True(top < middle - 60, $"top-aligned ink centre {top} is not above the middle {middle}");
        Assert.True(bottom > middle + 60, $"bottom-aligned ink centre {bottom} is not below the middle {middle}");
    }

    private static double InkCentreY(Func<IContainer, IContainer> align)
    {
        var png = KdpSoftShadow.Png(3, 1, 30, (c, color) => align(c).Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(30).FontColor(color));
            t.Span("Soft");
        }), "#FF0000");

        using var bitmap = SKBitmap.Decode(png);
        int first = bitmap.Height, last = -1;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 40)
                {
                    first = Math.Min(first, y);
                    last = Math.Max(last, y);
                    break;
                }
            }
        }

        Assert.True(last >= 0, "the shadow has no ink");
        return (first + last) / 2.0;
    }

    [Fact]
    public void A_saved_text_choice_is_drawn_in_its_box_and_colours()
    {
        var front = new KdpCoverText(new KdpTextBox(0.15, 0.55, 0.7, 0.2), "#FFEE00", "#112233");
        var back = new KdpCoverText(new KdpTextBox(0.3, 0.05, 0.6, 0.25), "#00FF00", "#445566");
        var settings = KdpBookSettings.Empty.WithCoverText(KdpPictureKeys.Front, front).WithCoverText(KdpPictureKeys.Back, back).WithDinoColors(new KdpTextColors("#0000FF", "#FFFFFF"));

        var result = KdpCover.Build(Input(settings));

        Assert.Empty(result.Warnings);
        using var pdf = PdfDocument.Open(result.Pdf);
        var page = pdf.GetPage(1);
        var frontTrim = KdpCoverSize.FrontTrim(24, ReadingDirection.LeftToRight);
        var backTrim = KdpCoverSize.BackTrim(24, ReadingDirection.LeftToRight);
        AssertWordsIn(page, "Freddy", KdpTextBoxMaths.Resolve(front.Box, frontTrim, null)!.Value, "#FFEE00");
        AssertWordsIn(page, "Author", KdpTextBoxMaths.Resolve(back.Box, backTrim, result.Barcode, KdpTextBoxMaths.QrStrip(backTrim))!.Value, "#00FF00");
        var dino = page.GetWords().First(w => w.Text == "Listen");
        Assert.All(dino.Letters, letter => Assert.True(InColour(letter, "#0000FF"), $"{letter.Value} is not blue"));
    }

    [Fact]
    public void A_text_box_that_is_unusable_falls_back_to_the_calm_band_and_says_so()
    {
        var tiny = new KdpCoverText(new KdpTextBox(0.1, 0.1, 0.05, 0.05), "#FFEE00", "#112233");
        var settings = KdpBookSettings.Empty.WithCoverText(KdpPictureKeys.Front, tiny);

        var result = KdpCover.Build(Input(settings));

        Assert.Contains(result.Warnings, w => w.Contains("front cover", StringComparison.Ordinal) && w.Contains("fallback", StringComparison.Ordinal));
        using var pdf = PdfDocument.Open(result.Pdf);
        Assert.Contains("Freddy", string.Join(' ', pdf.GetPage(1).GetWords().Select(w => w.Text)));
    }

    [Fact]
    public void A_box_shorter_than_the_gaps_between_the_credit_lines_gives_no_size_instead_of_throwing()
    {
        var lines = Enumerable.Range(1, 9).Select(n => $"Credit line {n}").ToList();

        var pt = KdpCoverDocument.TryChooseBackCreditPt(lines, new RectD(1, 1, 5, 0.3), rtl: false);

        Assert.Null(pt);
    }

    [Fact]
    public void A_title_too_long_for_its_saved_box_falls_back_instead_of_stopping_the_cover()
    {
        var narrow = new KdpCoverText(new KdpTextBox(0.1, 0.5, 0.2, 0.06), "#FFFFFF", "#000000");

        var result = KdpCover.Build(Input(KdpBookSettings.Empty.WithCoverText(KdpPictureKeys.Front, narrow)));

        Assert.Contains(result.Warnings, w => w.Contains("front cover", StringComparison.Ordinal));
    }

    [Fact]
    public void Text_choices_go_through_the_export_into_kdp_json_and_are_used_on_the_next_run()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            var choice = new KdpCoverText(new KdpTextBox(0.1, 0.6, 0.8, 0.2), "#ABCDEF", "#000000");
            var request = new KdpExportRequest(dino, "en", outFolder, CoverText: new Dictionary<string, KdpCoverText?> { [KdpPictureKeys.Front] = choice });

            KdpExporter.Export(request);
            var saved = KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName));
            KdpExporter.Export(request with { CoverText = null });
            var kept = KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName));
            KdpExporter.Export(request with { CoverText = new Dictionary<string, KdpCoverText?> { [KdpPictureKeys.Front] = null } });
            var cleared = KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName));

            Assert.Equal(choice with { ForOffsetPx = 0 }, saved.CoverText(KdpPictureKeys.Front));
            Assert.Equal(choice with { ForOffsetPx = 0 }, kept.CoverText(KdpPictureKeys.Front));
            Assert.Null(cleared.CoverText(KdpPictureKeys.Front));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_text_choice_given_with_an_over_max_offset_is_stamped_with_the_offset_used_and_is_not_stale()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            var choice = new KdpCoverText(new KdpTextBox(0.1, 0.6, 0.8, 0.2), "#ABCDEF", "#000000");
            var request = new KdpExportRequest(dino, "en", outFolder, CoverOffsets: new Dictionary<string, int> { [KdpPictureKeys.Front] = 99999 }, CoverText: new Dictionary<string, KdpCoverText?> { [KdpPictureKeys.Front] = choice });

            var first = KdpExporter.Export(request);
            var saved = KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName));
            var second = KdpExporter.Export(request with { CoverOffsets = new Dictionary<string, int> { [KdpPictureKeys.Front] = 88888 }, CoverText = null });

            var used = saved.CoverOffsetFor(KdpPictureKeys.Front);
            Assert.InRange(used, 1, 99998);
            Assert.Equal(used, saved.CoverText(KdpPictureKeys.Front)!.ForOffsetPx);
            Assert.DoesNotContain(first.Warnings, w => w.Contains("another crop", StringComparison.Ordinal));
            Assert.DoesNotContain(second.Warnings, w => w.Contains("another crop", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_text_choice_saved_beside_an_over_max_stored_offset_is_stamped_with_the_clamped_offset_and_is_not_stale_later()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            var settingsPath = Path.Combine(outFolder, KdpBookSettings.FileName);
            var choice = new KdpCoverText(new KdpTextBox(0.1, 0.6, 0.8, 0.2), "#ABCDEF", "#000000");
            KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, CoverOffsets: new Dictionary<string, int> { [KdpPictureKeys.Front] = 99999 }));
            var clamped = KdpBookSettings.Read(settingsPath).CoverOffsetFor(KdpPictureKeys.Front);
            KdpBookSettings.Read(settingsPath).WithCoverOffset(KdpPictureKeys.Front, clamped + 30).Write(settingsPath);

            var first = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, CoverText: new Dictionary<string, KdpCoverText?> { [KdpPictureKeys.Front] = choice }));
            var second = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder));

            Assert.Equal(clamped, KdpBookSettings.Read(settingsPath).CoverText(KdpPictureKeys.Front)!.ForOffsetPx);
            Assert.DoesNotContain(first.Warnings, w => w.Contains("another crop", StringComparison.Ordinal));
            Assert.DoesNotContain(second.Warnings, w => w.Contains("another crop", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_back_text_choice_for_a_book_with_no_back_cover_is_refused_and_kdp_json_is_not_touched()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            var choice = new KdpCoverText(new KdpTextBox(0.1, 0.1, 0.8, 0.2), "#ABCDEF", "#000000");

            var ex = Assert.Throws<KdpException>(() => KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, CoverText: new Dictionary<string, KdpCoverText?> { [KdpPictureKeys.Back] = choice })));

            Assert.Contains("back cover", ex.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(outFolder, KdpBookSettings.FileName)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_qr_code_on_the_last_interior_page_decodes_to_the_dino_reader_site_beside_the_new_line()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            var result = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder));

            using var pdf = PdfDocument.Open(result.PdfPath);
            var last = pdf.GetPage(pdf.NumberOfPages);
            var text = string.Join(' ', last.GetWords().Select(w => w.Text));
            Assert.Contains("Listen to this story in Dino", text);
            Assert.Contains("yourself!", text);
            var image = Assert.Single(last.GetImages());
            Assert.True(image.TryGetPng(out var png));
            Assert.Equal(KdpCoverWording.DinoReaderUrl, Decode(SKBitmap.Decode(png)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static int Count(string text, string word) => text.Split(word).Length - 1;

    private static bool IsQr(UglyToad.PdfPig.Content.IPdfImage image) =>
        Math.Abs(image.BoundingBox.Width - 0.8 * 72) < 2 && Math.Abs(image.BoundingBox.Height - 0.8 * 72) < 2;

    private static UglyToad.PdfPig.Content.IPdfImage Qr(UglyToad.PdfPig.Content.Page page) => Assert.Single(page.GetImages(), IsQr);

    /// <summary>The first letters of <paramref name="word"/> sit inside <paramref name="boxInches"/> (from the page's top-left) in the given colour.</summary>
    private static void AssertWordsIn(UglyToad.PdfPig.Content.Page page, string word, Core.Layout.RectD boxInches, string hex)
    {
        var found = page.GetWords().First(w => w.Text.StartsWith(word, StringComparison.Ordinal));
        var rect = found.BoundingBox;
        var top = page.Height - rect.Top;
        Assert.InRange(rect.Left, boxInches.X * 72 - 1, (boxInches.X + boxInches.Width) * 72 + 1);
        Assert.InRange(top, boxInches.Y * 72 - 1, (boxInches.Y + boxInches.Height) * 72 + 1);
        Assert.All(found.Letters, letter => Assert.True(InColour(letter, hex), $"{letter.Value} is not {hex}"));
    }

    private static bool InColour(UglyToad.PdfPig.Content.Letter letter, string hex)
    {
        var (r, g, b) = KdpHexColor.Rgb(hex);
        var (lr, lg, lb) = letter.Color.ToRGBValues();
        return Math.Abs((double)lr - r / 255.0) < 0.01 && Math.Abs((double)lg - g / 255.0) < 0.01 && Math.Abs((double)lb - b / 255.0) < 0.01;
    }

    [Fact]
    public void Long_back_cover_credits_shrink_to_fit_or_stop_the_cover_with_a_plain_message()
    {
        var longName = string.Join(", ", Enumerable.Range(1, 40).Select(i => $"Illustrator Number{i} Surname"));
        var input = Input() with { Credits = Credits with { Illustration = longName } };

        var error = Assert.Throws<KdpException>(() => KdpCover.Build(input));

        Assert.Contains("not fit", error.Message, StringComparison.Ordinal);
        Assert.Contains("shorten the names", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_words_box_with_no_width_beside_the_qr_code_stops_the_cover_with_a_plain_message()
    {
        var error = Assert.Throws<KdpException>(() => KdpCoverDocument.ChooseAppPt("Read it free", new RectD(1, 1, -0.3, 0.8), rtl: false));

        Assert.Contains("no room for the Dino line", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Moderately_long_back_cover_credits_still_build()
    {
        var input = Input() with { Credits = Credits with { Illustration = "Ashkan Sirous, Bea Longname-Surname and Carl Another-Longname" } };

        var result = KdpCover.Build(input);

        Assert.NotEmpty(result.Pdf);
    }

    [Fact]
    public void A_cover_without_a_back_picture_has_no_qr_code_and_no_shadowed_words_on_its_plain_panel()
    {
        var result = KdpCover.Build(Input(withBack: false));

        using var pdf = PdfDocument.Open(result.Pdf);
        var images = pdf.GetPage(1).GetImages().ToList();
        Assert.Equal(2, images.Count); // the front picture and the front title's soft shadow; the plain back has neither QR code nor shadow
        Assert.DoesNotContain(images, IsQr);
    }

    [Fact]
    public void The_saved_cover_offsets_move_the_crop_and_are_never_flagged()
    {
        var settings = KdpBookSettings.Empty.WithCoverOffset(KdpPictureKeys.Front, 100).WithCoverOffset(KdpPictureKeys.Back, 300);

        var result = KdpCover.Build(Input(settings));

        Assert.Equal(100, result.FrontPrepared!.AppliedOffsetPx);
        Assert.Equal(300, result.BackPrepared!.AppliedOffsetPx);
        Assert.True(result.FrontPrepared.OffsetChosen);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void A_cover_without_saved_offsets_keeps_the_top_crop()
    {
        var result = KdpCover.Build(Input());

        Assert.Equal(0, result.FrontPrepared!.AppliedOffsetPx);
        Assert.Equal(0, result.BackPrepared!.AppliedOffsetPx);
    }

    [Fact]
    public void A_cover_offset_past_the_picture_is_clamped_and_reported()
    {
        var result = KdpCover.Build(Input(KdpBookSettings.Empty.WithCoverOffset(KdpPictureKeys.Back, 99999)));

        Assert.Equal(582, result.BackPrepared!.AppliedOffsetPx);
        Assert.Contains(result.Warnings, w => w.StartsWith("Back cover: the offset of 99999 px is more than the picture allows", StringComparison.Ordinal));
    }

    [Fact]
    public void A_cover_offset_through_the_export_is_saved_and_used_on_the_next_run()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            var request = new KdpExportRequest(dino, "en", outFolder, CoverOffsets: new Dictionary<string, int> { [KdpPictureKeys.Front] = 120 });

            KdpExporter.Export(request);
            var saved = KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName));
            KdpExporter.Export(request with { CoverOffsets = null });

            Assert.Equal(120, saved.CoverOffsetFor(KdpPictureKeys.Front));
            Assert.Equal(120, KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName)).CoverOffsetFor(KdpPictureKeys.Front));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_back_offset_for_a_book_with_no_back_cover_is_refused_and_kdp_json_is_not_touched()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");

            var ex = Assert.Throws<KdpException>(() => KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, CoverOffsets: new Dictionary<string, int> { [KdpPictureKeys.Back] = 50 })));

            Assert.Contains("back cover", ex.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(outFolder, KdpBookSettings.FileName)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Clearing_the_back_offset_for_a_book_with_no_back_cover_is_harmless_and_a_stale_entry_is_warned_about()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            var stale = new KdpBookSettings().WithCoverOffset(KdpPictureKeys.Back, 40).ForBook("test1", "Freddy and the Test");
            stale.Write(Path.Combine(outFolder, KdpBookSettings.FileName));

            var warned = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder));
            var cleared = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, CoverOffsets: new Dictionary<string, int> { [KdpPictureKeys.Back] = 0 }));

            Assert.Contains(warned.Warnings, w => w.Contains("back cover", StringComparison.Ordinal) && w.Contains("kdp.json", StringComparison.Ordinal));
            Assert.DoesNotContain(cleared.Warnings, w => w.Contains("offset for the back cover", StringComparison.Ordinal));
            Assert.DoesNotContain(KdpPictureKeys.Back, KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName)).Covers);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void An_empty_back_entry_in_kdp_json_gives_no_warning_for_a_book_with_no_back_cover()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            File.WriteAllText(Path.Combine(outFolder, KdpBookSettings.FileName), """{"book":{"id":"test1","title":"Freddy and the Test"},"covers":{"back":{}}}""");

            var result = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder));

            Assert.DoesNotContain(result.Warnings, w => w.Contains("back cover", StringComparison.Ordinal) && w.Contains("kdp.json", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_soft_shadow_of_words_that_overflow_their_box_is_left_out_instead_of_misplaced()
    {
        var words = string.Join(' ', Enumerable.Repeat("overflowing", 40));

        var png = KdpSoftShadow.Png(1, 0.3, 20, (c, color) => c.Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(20).FontColor(color));
            t.Span(words);
        }), "#000000");

        Assert.Null(png);
    }

    [Fact]
    public void A_text_choice_made_for_the_offset_in_effect_is_used()
    {
        var box = new KdpCoverText(new KdpTextBox(0.15, 0.55, 0.7, 0.2), "#FFEE00", "#112233", ForOffsetPx: 40);
        var settings = KdpBookSettings.Empty.WithCoverOffset(KdpPictureKeys.Front, 40).WithCoverText(KdpPictureKeys.Front, box);

        var result = KdpCover.Build(Input(settings));

        Assert.DoesNotContain(result.Warnings, w => w.Contains("another crop", StringComparison.Ordinal));
        using var pdf = PdfDocument.Open(result.Pdf);
        AssertWordsIn(pdf.GetPage(1), "Freddy", KdpTextBoxMaths.Resolve(box.Box, KdpCoverSize.FrontTrim(24, ReadingDirection.LeftToRight), null)!.Value, "#FFEE00");
    }

    [Fact]
    public void A_text_choice_made_for_another_offset_falls_back_and_warns()
    {
        var box = new KdpCoverText(new KdpTextBox(0.15, 0.55, 0.7, 0.2), "#FFEE00", "#112233", ForOffsetPx: 40);
        var settings = KdpBookSettings.Empty.WithCoverOffset(KdpPictureKeys.Front, 80).WithCoverText(KdpPictureKeys.Front, box);

        var result = KdpCover.Build(Input(settings));

        Assert.Contains(result.Warnings, w => w.Contains("front cover", StringComparison.Ordinal) && w.Contains("another crop", StringComparison.Ordinal) && w.Contains("fallback", StringComparison.Ordinal));
        using var pdf = PdfDocument.Open(result.Pdf);
        var title = pdf.GetPage(1).GetWords().First(w => w.Text == "Freddy");
        Assert.All(title.Letters, letter => Assert.False(InColour(letter, "#FFEE00"), "the stale choice's colour was used"));
    }

    private static bool IsWhiteFill(UglyToad.PdfPig.Graphics.PdfPath path)
    {
        return path.IsFilled && path.FillColor is { } colour && colour.ToRGBValues() is var (r, g, b) && r > 0.99 && g > 0.99 && b > 0.99;
    }

    /// <summary>The cover drawn to pixels at 300 dpi, the way KDP prints it.</summary>
    private static SKBitmap RenderedCover(KdpCoverInput input)
    {
        KdpCover.Build(input);
        var settings = input.Settings ?? KdpBookSettings.Empty;
        var front = KdpImageDecoder.Prepare(input.FrontPicture, settings.CoverOffsetFor(KdpPictureKeys.Front), settings.HasCoverOffset(KdpPictureKeys.Front));
        var back = KdpImageDecoder.Prepare(input.BackPicture!, settings.CoverOffsetFor(KdpPictureKeys.Back), settings.HasCoverOffset(KdpPictureKeys.Back));
        var drawing = new KdpCoverDrawing(input.Title, input.Language, input.Credits, front, back, input.PageCount, input.Direction);
        var png = KdpCoverDocument.Create(drawing).GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 300 }).Single();
        using var whole = SKBitmap.Decode(png);
        return LowerLeftOfTheBack(whole);
    }

    /// <summary>The corner of the back where the QR code sits (the scanner finds it faster and more surely in a small area than in the whole wrap).</summary>
    private static SKBitmap LowerLeftOfTheBack(SKBitmap whole)
    {
        var corner = new SKBitmap(1500, 700);
        whole.ExtractSubset(corner, new SKRectI(0, whole.Height - 700, 1500, whole.Height));
        return corner;
    }

    private static string? Decode(SKBitmap bitmap)
    {
        using var rgba = bitmap.Copy(SKColorType.Rgba8888);
        var source = new RGBLuminanceSource(rgba.Bytes, rgba.Width, rgba.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
        var reader = new BarcodeReaderGeneric { Options = new DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] } };
        return reader.Decode(source)?.Text;
    }
}
