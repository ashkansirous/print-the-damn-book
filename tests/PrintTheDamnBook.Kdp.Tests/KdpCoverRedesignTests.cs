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
        var qr = Assert.Single(pdf.GetPage(1).GetImages(), image => image.WidthInSamples < 1000);
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
        var qr = Assert.Single(pdf.GetPage(1).GetImages(), image => image.WidthInSamples < 1000);
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
    public void The_front_has_the_title_and_the_written_by_illustrated_by_line_and_the_back_the_credit_lines()
    {
        var result = KdpCover.Build(Input());

        using var pdf = PdfDocument.Open(result.Pdf);
        var text = string.Join(' ', pdf.GetPage(1).GetWords().Select(w => w.Text));
        Assert.Contains("Written", text);
        Assert.Contains("Illustrated", text);
        Assert.Contains("Author", text);
        Assert.Contains("Illustration:", text);
        Assert.Contains("speak", text);
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
    public void A_front_credit_that_wraps_at_every_size_stops_the_cover_with_a_plain_message()
    {
        var longCredit = string.Join(", ", Enumerable.Range(1, 40).Select(i => $"Illustrator Number{i} Surname"));

        var error = Assert.Throws<KdpException>(() => KdpCoverDocument.ChooseCreditPt(longCredit, 7.5, rtl: false));

        Assert.Contains("front credit does not fit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_front_credit_that_wraps_but_fits_gets_a_size_that_fits()
    {
        var credit = "Written by Ann Author-Longname and Bea Another-Longname-Surname, Illustrated by Ashkan Sirous and Carl Third-Longname";

        var pt = KdpCoverDocument.ChooseCreditPt(credit, 6.0, rtl: false);

        Assert.InRange(pt, 11, 15);
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
        Assert.Single(pdf.GetPage(1).GetImages());
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
