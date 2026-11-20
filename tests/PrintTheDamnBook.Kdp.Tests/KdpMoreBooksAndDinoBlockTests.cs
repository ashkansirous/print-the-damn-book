using PrintTheDamnBook.Cli;
using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp.Cover;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>Slice 41: the Dino block (QR code and line) placed in a saved box on the back cover, and the "More books" block on the last interior page.</summary>
public class KdpMoreBooksAndDinoBlockTests
{
    private static readonly DinoCredits Credits = new("Ann Author", false, "Ashkan Sirous", 2026, 11);
    private static readonly KdpTextBox MiddleBox = new(0.1, 0.45, 0.6, 0.15);

    private static KdpCoverInput Input(KdpBookSettings settings, bool rtl = false) =>
        new("Freddy and the Test", rtl ? "fa" : "en", Credits, SyntheticBook.Picture(1), SyntheticBook.Picture(2), 24, rtl ? ReadingDirection.RightToLeft : ReadingDirection.LeftToRight, settings);

    private static KdpBookSettings WithDino(KdpTextBox box, int forOffset = 0) =>
        KdpBookSettings.Empty.WithDinoText(new KdpTextColors("#FFFFFF", "#1A2440"), box, forOffset);

    private static bool IsQr(IPdfImage image) => Math.Abs(image.BoundingBox.Width - 0.8 * 72) < 2;

    private static (double Left, double Top, double Right, double Bottom) Inches(IPdfImage image, Page page) =>
        (image.BoundingBox.Left / 72, (page.Height - image.BoundingBox.Top) / 72, image.BoundingBox.Right / 72, (page.Height - image.BoundingBox.Bottom) / 72);

    [Fact]
    public void A_saved_dino_box_holds_the_qr_code_at_its_left_edge_and_the_line_beside_it()
    {
        var result = KdpCover.Build(Input(WithDino(MiddleBox)));

        Assert.Empty(result.Warnings);
        using var pdf = PdfDocument.Open(result.Pdf);
        var page = pdf.GetPage(1);
        var box = KdpTextBoxMaths.ResolveBlock(MiddleBox, KdpCoverSize.BackTrim(24, ReadingDirection.LeftToRight), result.Barcode, KdpCoverSize.BackPanel(24, ReadingDirection.LeftToRight))!.Value;
        var qr = Inches(Assert.Single(page.GetImages(), IsQr), page);
        Assert.Equal(box.X, qr.Left, 0.03);
        Assert.Equal(box.Y + (box.Height - 0.8) / 2, qr.Top, 0.03);
        var line = page.GetWords().First(w => w.Text == "Listen");
        Assert.InRange(line.BoundingBox.Left / 72, qr.Right, box.X + box.Width);
        Assert.InRange((page.Height - line.BoundingBox.Top) / 72, box.Y - 0.02, box.Y + box.Height);
    }

    [Fact]
    public void A_saved_dino_box_leaves_the_bottom_corner_free()
    {
        var result = KdpCover.Build(Input(WithDino(MiddleBox)));

        using var pdf = PdfDocument.Open(result.Pdf);
        var page = pdf.GetPage(1);
        var corner = KdpCoverSize.BackTrim(24, ReadingDirection.LeftToRight);
        Assert.DoesNotContain(page.GetImages().Where(IsQr), image => Inches(image, page).Top > corner.Y + corner.Height - 1.5);
    }

    [Fact]
    public void A_right_to_left_cover_puts_the_qr_code_at_the_right_edge_of_the_box()
    {
        var result = KdpCover.Build(Input(WithDino(MiddleBox), rtl: true));

        using var pdf = PdfDocument.Open(result.Pdf);
        var page = pdf.GetPage(1);
        var box = KdpTextBoxMaths.ResolveBlock(MiddleBox, KdpCoverSize.BackTrim(24, ReadingDirection.RightToLeft), result.Barcode, KdpCoverSize.BackPanel(24, ReadingDirection.RightToLeft))!.Value;
        var qr = Inches(Assert.Single(page.GetImages(), IsQr), page);
        Assert.Equal(box.X + box.Width, qr.Right, 0.03);
    }

    [Fact]
    public void A_dino_box_that_meets_the_credit_lines_falls_back_to_the_corner_and_says_so()
    {
        var overCredits = new KdpTextBox(0.1, 0.1, 0.6, 0.15);

        var result = KdpCover.Build(Input(WithDino(overCredits)));

        Assert.Contains(result.Warnings, w => w.Contains("Dino block", StringComparison.Ordinal) && w.Contains("credit lines", StringComparison.Ordinal));
        AssertQrInCorner(result);
    }

    [Fact]
    public void A_dino_box_too_small_for_the_qr_code_and_line_falls_back_to_the_corner_and_says_so()
    {
        var result = KdpCover.Build(Input(WithDino(new KdpTextBox(0.1, 0.45, 0.2, 0.15))));

        Assert.Contains(result.Warnings, w => w.Contains("Dino block", StringComparison.Ordinal) && w.Contains("bottom corner", StringComparison.Ordinal));
        AssertQrInCorner(result);
    }

    [Fact]
    public void Drawing_the_same_cover_twice_does_not_repeat_a_warning()
    {
        var (drawing, _, _, warnings) = KdpCover.Prepare(Input(WithDino(new KdpTextBox(0.1, 0.45, 0.2, 0.15))));

        KdpCoverDocument.Render(drawing);
        KdpCoverDocument.Render(drawing);

        Assert.Equal(warnings.Count, warnings.Distinct().Count());
        Assert.Single(warnings, w => w.Contains("bottom corner", StringComparison.Ordinal));
    }

    [Fact]
    public void A_bad_other_books_path_is_a_warning_and_the_export_still_writes_every_file()
    {
        var folder = Books(1);
        try
        {
            foreach (var bad in new[] { "bad\0folder", "" })
            {
                var result = KdpExporter.Export(new KdpExportRequest(Path.Combine(folder, "book-t00.dino"), "en", Path.Combine(folder, "out"), OtherBooksFolder: bad));

                Assert.True(File.Exists(result.PdfPath));
                Assert.Contains(result.Warnings, w => w.Contains("other books", StringComparison.Ordinal));
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_dino_box_made_for_another_crop_falls_back_to_the_corner_and_says_so()
    {
        var settings = KdpBookSettings.Empty.WithCoverOffset(KdpPictureKeys.Back, 80).WithDinoText(new KdpTextColors("#FFFFFF", "#000000"), MiddleBox, 40);

        var result = KdpCover.Build(Input(settings));

        Assert.Contains(result.Warnings, w => w.Contains("Dino block", StringComparison.Ordinal) && w.Contains("another crop", StringComparison.Ordinal));
        AssertQrInCorner(result);
    }

    [Fact]
    public void Colours_only_keep_todays_corner_placement_without_a_warning()
    {
        var result = KdpCover.Build(Input(KdpBookSettings.Empty.WithDinoColors(new KdpTextColors("#FFFFFF", "#000000"))));

        Assert.Empty(result.Warnings);
        AssertQrInCorner(result);
    }

    [Fact]
    public void Credits_may_use_the_bottom_strip_once_the_dino_block_is_placed_elsewhere()
    {
        var lowCredits = new KdpCoverText(new KdpTextBox(0.15, 0.78, 0.5, 0.1), "#FFFFFF", "#000000");
        var settings = WithDino(MiddleBox).WithCoverText(KdpPictureKeys.Back, lowCredits);

        var result = KdpCover.Build(Input(settings));

        Assert.Empty(result.Warnings);
        using var pdf = PdfDocument.Open(result.Pdf);
        var page = pdf.GetPage(1);
        var credit = page.GetWords().First(w => w.Text.StartsWith("Author", StringComparison.Ordinal));
        Assert.True((page.Height - credit.BoundingBox.Top) / 72 > 6.5, "the credits were not placed in the bottom strip");
    }

    private static void AssertQrInCorner(KdpCoverResult result)
    {
        using var pdf = PdfDocument.Open(result.Pdf);
        var page = pdf.GetPage(1);
        var trim = KdpCoverSize.BackTrim(24, ReadingDirection.LeftToRight);
        var qr = Inches(Assert.Single(page.GetImages(), IsQr), page);
        Assert.Equal(trim.X + KdpTextBoxMaths.PanelInsetInches, qr.Left, 0.03);
        Assert.Equal(trim.Y + trim.Height - KdpTextBoxMaths.PanelInsetInches - 0.8, qr.Top, 0.03);
    }

    private static KdpBookInput WithOthers(int others)
    {
        var book = SyntheticBook.Input(2);
        return book with { OtherCovers = Enumerable.Range(0, others).Select(i => SyntheticBook.Picture(i, 400, 400)).ToList() };
    }

    [Fact]
    public void The_last_page_shows_the_heading_the_covers_and_the_qr_code_and_the_page_count_does_not_change()
    {
        var plain = KdpInterior.Build(SyntheticBook.Input(2));
        var withOthers = KdpInterior.Build(WithOthers(15));

        Assert.Equal(plain.Plan.PageCount, withOthers.Plan.PageCount);
        using var pdf = PdfDocument.Open(withOthers.Pdf);
        var last = pdf.GetPage(withOthers.Plan.PageCount);
        var images = last.GetImages().ToList();
        Assert.Equal(16, images.Count);
        Assert.Single(images, image => Math.Abs(image.BoundingBox.Width - 1.1 * 72) < 2);
        Assert.Equal(15, images.Count(image => Math.Abs(image.BoundingBox.Width - KdpOtherBooks.TilePt) < 2));
        var text = string.Join(' ', last.GetWords().Select(w => w.Text));
        Assert.Equal(KdpFrontMatterWording.OtherBooksHeadingText + " " + KdpCoverWording.DinoLine, text);
    }

    [Fact]
    public void The_qr_code_on_the_last_page_still_decodes_with_other_books_above_it()
    {
        var result = KdpInterior.Build(WithOthers(15));

        using var pdf = PdfDocument.Open(result.Pdf);
        var qr = Assert.Single(pdf.GetPage(result.Plan.PageCount).GetImages(), image => Math.Abs(image.BoundingBox.Width - 1.1 * 72) < 2);
        Assert.Equal(KdpCoverWording.DinoReaderUrl, DecodeQr(qr));
    }

    [Fact]
    public void The_covers_sit_five_to_a_row_inside_the_safe_margin_above_the_qr_code()
    {
        var result = KdpInterior.Build(WithOthers(12));

        using var pdf = PdfDocument.Open(result.Pdf);
        var page = pdf.GetPage(result.Plan.PageCount);
        var tiles = page.GetImages().Where(image => Math.Abs(image.BoundingBox.Width - KdpOtherBooks.TilePt) < 2).ToList();
        var qr = page.GetImages().Single(image => Math.Abs(image.BoundingBox.Width - 1.1 * 72) < 2);
        Assert.Equal(12, tiles.Count);
        Assert.Equal([5, 5, 2], tiles.GroupBy(t => Math.Round(t.BoundingBox.Bottom)).OrderByDescending(g => g.Key).Select(g => g.Count()).ToArray());
        Assert.All(tiles, tile => Assert.True(tile.BoundingBox.Bottom > qr.BoundingBox.Top));
        Assert.All(tiles, tile => Assert.InRange(tile.BoundingBox.Left, 0.5 * 72 - 1, page.Width - 0.5 * 72 + 1));
    }

    [Fact]
    public void Without_other_books_the_last_page_is_as_before_the_qr_code_and_the_line_only()
    {
        var result = KdpInterior.Build(SyntheticBook.Input(2));

        using var pdf = PdfDocument.Open(result.Pdf);
        var last = pdf.GetPage(result.Plan.PageCount);
        Assert.Single(last.GetImages());
        Assert.Equal(KdpCoverWording.DinoLine, string.Join(' ', last.GetWords().Select(w => w.Text)));
    }

    [Fact]
    public void The_loader_takes_fifteen_of_the_other_books_never_this_one_and_the_same_ones_on_a_rebuild()
    {
        var folder = Books(17);
        try
        {
            var first = KdpOtherBooksLoader.Load(folder, folder, "t00", "en");
            var again = KdpOtherBooksLoader.Load(folder, folder, "t00", "en");

            Assert.Equal(15, first.Tiles.Count);
            Assert.Empty(first.Warnings);
            Assert.Equal(first.Tiles.Select(t => t.Jpeg.Length), again.Tiles.Select(t => t.Jpeg.Length));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void An_other_books_own_kdp_json_found_by_book_id_changes_its_tile()
    {
        var folder = Books(2);
        var outputs = Path.Combine(folder, "outputs");
        Directory.CreateDirectory(Path.Combine(outputs, "t01-book"));
        try
        {
            var before = KdpOtherBooksLoader.Load(folder, outputs, "t00", "en").Tiles.Single();
            KdpBookSettings.Empty.WithCoverOffset(KdpPictureKeys.Front, 600).ForBook("t01", "Book t01").Write(Path.Combine(outputs, "t01-book", KdpBookSettings.FileName));

            var after = KdpOtherBooksLoader.Load(folder, outputs, "t00", "en").Tiles.Single();

            Assert.NotEqual(before.Jpeg, after.Jpeg);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_file_that_is_not_a_book_is_skipped_with_a_warning_and_a_missing_folder_stops_the_run()
    {
        var folder = Books(2);
        File.WriteAllText(Path.Combine(folder, "broken.dino"), "not a zip");
        try
        {
            var result = KdpOtherBooksLoader.Load(folder, folder, "t00", "en");

            Assert.Single(result.Tiles);
            Assert.Contains(result.Warnings, w => w.Contains("broken.dino", StringComparison.Ordinal));
            Assert.Throws<KdpException>(() => KdpOtherBooksLoader.Load(Path.Combine(folder, "nope"), folder, "t00", "en"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_export_puts_the_other_books_on_the_last_page_without_changing_the_page_count()
    {
        var folder = Books(4);
        try
        {
            var plain = KdpExporter.Export(new KdpExportRequest(Path.Combine(folder, "book-t00.dino"), "en", Path.Combine(folder, "out-plain")));
            var withOthers = KdpExporter.Export(new KdpExportRequest(Path.Combine(folder, "book-t00.dino"), "en", Path.Combine(folder, "out-others"), OtherBooksFolder: folder));

            Assert.Equal(plain.PageCount, withOthers.PageCount);
            Assert.Null(plain.OtherBookTitles);
            Assert.Equal(3, withOthers.OtherBookTitles!.Count);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_missing_other_books_folder_is_a_warning_and_the_export_still_writes_every_file()
    {
        var folder = Books(1);
        try
        {
            var result = KdpExporter.Export(new KdpExportRequest(Path.Combine(folder, "book-t00.dino"), "en", Path.Combine(folder, "out"), OtherBooksFolder: Path.Combine(folder, "nope")));

            Assert.True(File.Exists(result.PdfPath));
            Assert.True(File.Exists(result.ReportPath));
            Assert.Contains(result.Warnings, w => w.Contains("other books", StringComparison.Ordinal));
            Assert.Contains("other books", File.ReadAllText(result.ReportPath), StringComparison.Ordinal);
            Assert.Empty(result.OtherBookTitles!);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_book_with_one_output_folder_per_language_uses_the_kdp_json_made_for_this_language_every_time()
    {
        var folder = Books(2);
        var outputs = Path.Combine(folder, "outputs");
        var plain = KdpOtherBooksLoader.Load(folder, outputs, "t00", "en").Tiles.Single();
        WriteSettings(outputs, "t01-fa", 600);
        WriteSettings(outputs, "t01-en", 0);
        try
        {
            var english = KdpOtherBooksLoader.Load(folder, outputs, "t00", "en").Tiles.Single();
            var again = KdpOtherBooksLoader.Load(folder, outputs, "t00", "en").Tiles.Single();
            var farsi = KdpOtherBooksLoader.Load(folder, outputs, "t00", "fa").Tiles.Single();

            Assert.Equal(english.Jpeg, again.Jpeg);
            Assert.Equal(plain.Jpeg, english.Jpeg);
            Assert.NotEqual(english.Jpeg, farsi.Jpeg);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_dino_file_with_a_capital_extension_is_found()
    {
        var folder = Books(1);
        SyntheticBook.WriteDino(Path.Combine(folder, "Other.DINO"), pages: 1, id: "t09", title: "Book t09");
        try
        {
            Assert.Single(KdpOtherBooksLoader.Load(folder, folder, "t00", "en").Tiles);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Books_out_without_other_books_is_refused()
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--books-out", "outs"]);

        Assert.Null(parsed.Request);
        Assert.NotNull(parsed.Error);
    }

    private static void WriteSettings(string outputs, string name, int frontOffset)
    {
        Directory.CreateDirectory(Path.Combine(outputs, name));
        KdpBookSettings.Empty.WithCoverOffset(KdpPictureKeys.Front, frontOffset).ForBook("t01", "Book t01").Write(Path.Combine(outputs, name, KdpBookSettings.FileName));
    }

    [Fact]
    public void The_command_line_reads_a_dino_box_with_colours_and_the_other_books_folders()
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--dino-text", "back=0.1,0.45,0.6,0.15,#ffffff,#1a2440", "--other-books", "books", "--books-out", "outs"]);

        Assert.Null(parsed.Error);
        Assert.Equal(new KdpDinoTextChange(new KdpTextColors("#FFFFFF", "#1A2440"), new KdpTextBox(0.1, 0.45, 0.6, 0.15)), parsed.Request!.DinoText);
        Assert.Equal("books", parsed.Request.OtherBooksFolder);
        Assert.Equal("outs", parsed.Request.BooksOutRoot);
    }

    [Theory]
    [InlineData("--dino-text", "back=0.1,0.45,0.95,0.15,#ffffff,#1a2440")]
    [InlineData("--dino-text", "back=0.1,0.45,0.6,0.15,#ffffff")]
    [InlineData("--dino-text", "back=a,0.45,0.6,0.15,#ffffff,#000000")]
    [InlineData("--other-books", "--out")]
    public void A_wrong_dino_box_or_missing_folder_is_refused(string option, string value)
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", option, value]);

        Assert.Null(parsed.Request);
        Assert.NotNull(parsed.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void The_outputs_root_is_the_parent_of_the_output_folder_with_or_without_a_trailing_separator(string trailing)
    {
        var parent = Path.Combine(Path.GetTempPath(), "ptdb-outputs");
        var folder = Path.Combine(parent, "book-en") + trailing;

        Assert.Equal(parent, KdpExporter.OutputsRootOf(folder));
    }

    [Fact]
    public void A_book_whose_cover_picture_cannot_be_drawn_is_skipped_with_a_warning_and_the_export_still_writes_every_file()
    {
        var folder = Books(3);
        CorruptPicture(Path.Combine(folder, "book-t01.dino"));
        try
        {
            var result = KdpExporter.Export(new KdpExportRequest(Path.Combine(folder, "book-t00.dino"), "en", Path.Combine(folder, "out") + Path.DirectorySeparatorChar, OtherBooksFolder: folder));

            Assert.True(File.Exists(result.PdfPath));
            Assert.Equal(["Book t02"], result.OtherBookTitles!);
            Assert.Contains(result.Warnings, w => w.Contains("Book t01", StringComparison.Ordinal) && w.Contains("skipped", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_book_with_no_cover_and_no_pages_is_left_out_with_a_warning()
    {
        var folder = Books(2);
        SyntheticBook.WriteDino(Path.Combine(folder, "book-empty.dino"), pages: 0, id: "t09", title: "Book empty");
        try
        {
            var result = KdpOtherBooksLoader.Load(folder, folder, "t00", "en");

            Assert.Single(result.Tiles);
            Assert.Contains(result.Warnings, w => w.Contains("book-empty.dino", StringComparison.Ordinal) || w.Contains("Book empty", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void CorruptPicture(string dinoPath)
    {
        using var zip = System.IO.Compression.ZipFile.Open(dinoPath, System.IO.Compression.ZipArchiveMode.Update);
        zip.GetEntry("page-1.jpg")!.Delete();
        using var entry = zip.CreateEntry("page-1.jpg").Open();
        entry.Write("this is not a picture"u8);
    }

    /// <summary>A folder with <paramref name="count"/> one-page books, ids t00, t01 ... and files book-t00.dino ...</summary>
    private static string Books(int count)
    {
        var folder = SyntheticBook.NewFolder();
        for (var i = 0; i < count; i++)
        {
            SyntheticBook.WriteDino(Path.Combine(folder, $"book-t{i:00}.dino"), pages: 1, id: $"t{i:00}", title: $"Book t{i:00}");
        }

        return folder;
    }

    private static string? DecodeQr(IPdfImage image)
    {
        Assert.True(image.TryGetPng(out var png));
        using var bitmap = SkiaSharp.SKBitmap.Decode(png);
        using var rgba = bitmap.Copy(SkiaSharp.SKColorType.Rgba8888);
        var source = new ZXing.RGBLuminanceSource(rgba.Bytes, rgba.Width, rgba.Height, ZXing.RGBLuminanceSource.BitmapFormat.RGBA32);
        var reader = new ZXing.BarcodeReaderGeneric { Options = new ZXing.Common.DecodingOptions { TryHarder = true, PossibleFormats = [ZXing.BarcodeFormat.QR_CODE] } };
        return reader.Decode(source)?.Text;
    }
}
