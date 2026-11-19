using PrintTheDamnBook.Cli;
using PrintTheDamnBook.Core.Kdp;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp.Tests;

public class KdpPicturesTests
{
    [Fact]
    public void Art_crossing_the_cut_line_is_flagged_with_a_preview()
    {
        var picture = KdpImageDecoder.Prepare(CharacterAcrossTheCut());

        Assert.True(picture.Flagged);
        Assert.NotNull(picture.Preview);
        using var preview = SKBitmap.Decode(picture.Preview);
        Assert.Equal(Pictures.CutLineInputs.PreviewWidthPx, preview.Width);
    }

    [Fact]
    public void A_plain_picture_is_not_flagged_and_has_no_preview()
    {
        var picture = KdpImageDecoder.Prepare(SyntheticBook.Picture(0));

        Assert.False(picture.Flagged);
        Assert.Null(picture.Preview);
    }

    [Fact]
    public void A_page_with_a_chosen_offset_is_never_flagged_over_it()
    {
        var picture = KdpImageDecoder.Prepare(CharacterAcrossTheCut(), offsetPx: 300, offsetChosen: true);

        Assert.False(picture.Flagged);
        Assert.Null(picture.Preview);
        Assert.Equal(300, picture.Crop.Source.Y);
    }

    [Fact]
    public void Moving_the_crop_down_lets_the_picture_stay_2588_by_2625()
    {
        var picture = KdpImageDecoder.Prepare(CharacterAcrossTheCut(), offsetPx: 300, offsetChosen: true);

        using var decoded = SKBitmap.Decode(picture.Jpeg);
        Assert.Equal(2588, decoded.Width);
        Assert.Equal(2625, decoded.Height);
    }

    [Fact]
    public void The_offset_option_is_parsed_and_repeatable()
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--offset", "7=60", "--offset", "2=0"]);

        Assert.Null(parsed.Error);
        Assert.Equal(60, parsed.Request!.Offsets![7]);
        Assert.Equal(0, parsed.Request.Offsets[2]);
    }

    [Theory]
    [InlineData("7")]
    [InlineData("0=5")]
    [InlineData("x=5")]
    [InlineData("7=-5")]
    [InlineData("7=5=6")]
    public void A_bad_offset_is_named(string value)
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--offset", value]);

        Assert.Null(parsed.Request);
        Assert.Contains("--offset", parsed.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void An_offset_on_the_command_line_is_saved_to_kdp_json_and_used_again_on_the_next_run()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");

            Assert.Equal(KdpRunner.Ok, KdpRunner.Run(["kdp", dino, "--out", outFolder, "--offset", "2=40"], new StringWriter(), new StringWriter()));
            Assert.Equal(40, KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName)).OffsetFor(2));
            Assert.Equal(KdpRunner.Ok, KdpRunner.Run(["kdp", dino, "--out", outFolder], new StringWriter(), new StringWriter()));

            var report = File.ReadAllText(Path.Combine(outFolder, KdpExporter.ReportName));
            Assert.Contains("| 2 | 1792 x 2400 | 0, 40, 1792 x 1818 |", report, StringComparison.Ordinal);
            Assert.Contains("chosen offset 40 px", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void An_offset_for_a_page_the_book_does_not_have_fails_with_exit_code_1()
    {
        var folder = SyntheticBook.NewFolder();
        var error = new StringWriter();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);

            var code = KdpRunner.Run(["kdp", dino, "--out", Path.Combine(folder, "out"), "--offset", "9=10"], new StringWriter(), error);

            Assert.Equal(KdpRunner.Failed, code);
            Assert.Contains("page 9", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("with a picture", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void An_offset_on_a_picture_with_no_spare_height_warns_and_is_not_saved()
    {
        var folder = SyntheticBook.NewFolder();
        var error = new StringWriter();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 2, pictureWidth: 2400, pictureHeight: 1792);
            var outFolder = Path.Combine(folder, "out");

            Assert.Equal(KdpRunner.Ok, KdpRunner.Run(["kdp", dino, "--out", outFolder, "--offset", "1=80"], new StringWriter(), error));

            Assert.Contains("Story page 1: the offset of 80 px has no effect", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("An --offset for it is not saved", error.ToString(), StringComparison.Ordinal);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(error.ToString(), "Story page 1: the offset of 80 px"));
            Assert.False(File.Exists(Path.Combine(outFolder, KdpBookSettings.FileName)));
            Assert.Contains("ignored (no spare height)", File.ReadAllText(Path.Combine(outFolder, KdpExporter.ReportName)), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void An_offset_past_the_largest_possible_is_clamped_in_the_warning_report_and_saved_file()
    {
        var folder = SyntheticBook.NewFolder();
        var error = new StringWriter();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 2);
            var outFolder = Path.Combine(folder, "out");

            Assert.Equal(KdpRunner.Ok, KdpRunner.Run(["kdp", dino, "--out", outFolder, "--offset", "1=900"], new StringWriter(), error));

            Assert.Contains("is more than the picture allows; 582 px was used", error.ToString(), StringComparison.Ordinal);
            Assert.Equal(582, KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName)).OffsetFor(1));
            Assert.Contains("chosen offset 582 px (asked 900, clamped)", File.ReadAllText(Path.Combine(outFolder, KdpExporter.ReportName)), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void An_offset_of_zero_clears_the_saved_choice_and_a_saved_page_past_the_end_is_warned_about()
    {
        var folder = SyntheticBook.NewFolder();
        var error = new StringWriter();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 2);
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            var path = Path.Combine(outFolder, KdpBookSettings.FileName);
            KdpBookSettings.Empty.WithOffset(1, 40).WithOffset(7, 20).Write(path);

            Assert.Equal(KdpRunner.Ok, KdpRunner.Run(["kdp", dino, "--out", outFolder, "--offset", "1=0"], new StringWriter(), error));

            var saved = KdpBookSettings.Read(path);
            Assert.False(saved.HasOffset(1));
            Assert.True(saved.HasOffset(7));
            Assert.Contains("kdp.json has an offset for story page 7, but the book has 2 story pages", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_broken_kdp_json_stops_the_run_and_is_left_as_it_is()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            File.WriteAllText(Path.Combine(outFolder, KdpBookSettings.FileName), "{ broken");

            var code = KdpRunner.Run(["kdp", dino, "--out", outFolder, "--offset", "1=5"], new StringWriter(), new StringWriter());

            Assert.Equal(KdpRunner.Failed, code);
            Assert.Equal("{ broken", File.ReadAllText(Path.Combine(outFolder, KdpBookSettings.FileName)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_failed_write_of_the_pdf_leaves_the_old_kdp_json_untouched()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            Assert.Equal(KdpRunner.Ok, KdpRunner.Run(["kdp", dino, "--out", outFolder, "--offset", "2=40"], new StringWriter(), new StringWriter()));
            var path = Path.Combine(outFolder, KdpBookSettings.FileName);
            var before = File.ReadAllText(path);
            File.Delete(Path.Combine(outFolder, KdpExporter.PdfName));
            Directory.CreateDirectory(Path.Combine(outFolder, KdpExporter.PdfName));

            Assert.ThrowsAny<Exception>(() => KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, new Dictionary<int, int> { [2] = 90 })));

            Assert.Equal(before, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_kdp_json_made_for_another_book_stops_the_run_and_is_left_as_it_is()
    {
        var folder = SyntheticBook.NewFolder();
        var error = new StringWriter();
        try
        {
            var first = SyntheticBook.WriteDino(Path.Combine(folder, "a.dino"), 3, id: "book-a");
            var second = SyntheticBook.WriteDino(Path.Combine(folder, "b.dino"), 3, id: "book-b");
            var outFolder = Path.Combine(folder, "out");
            Assert.Equal(KdpRunner.Ok, KdpRunner.Run(["kdp", first, "--out", outFolder, "--offset", "2=40"], new StringWriter(), new StringWriter()));
            var path = Path.Combine(outFolder, KdpBookSettings.FileName);
            var before = File.ReadAllText(path);

            var code = KdpRunner.Run(["kdp", second, "--out", outFolder, "--offset", "1=10"], new StringWriter(), error);

            Assert.Equal(KdpRunner.Failed, code);
            Assert.Contains("another --out folder", error.ToString(), StringComparison.Ordinal);
            Assert.Equal(before, File.ReadAllText(path));
            Assert.Equal(KdpRunner.Ok, KdpRunner.Run(["kdp", first, "--out", outFolder], new StringWriter(), new StringWriter()));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_saved_offset_for_a_page_with_no_picture_is_warned_about()
    {
        var folder = SyntheticBook.NewFolder();
        var error = new StringWriter();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3, withoutPicture: 1);
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            KdpBookSettings.Empty.WithOffset(3, 20).Write(Path.Combine(outFolder, KdpBookSettings.FileName));

            Assert.Equal(KdpRunner.Ok, KdpRunner.Run(["kdp", dino, "--out", outFolder], new StringWriter(), error));

            Assert.Contains("kdp.json has an offset for story page 3, but the book has 2 story pages", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_report_lists_scale_flag_and_preview_for_a_flagged_page()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var input = SyntheticBook.Input(4);
            var pictures = input.Pictures.ToList();
            pictures[1] = CharacterAcrossTheCut();
            var result = KdpInterior.Build(input with { Pictures = pictures });

            var report = KdpCheckReport.Build("T", "en", result);

            Assert.Contains("Scale", report, StringComparison.Ordinal);
            Assert.Contains("1.44x", report, StringComparison.Ordinal);
            Assert.Contains("FLAGGED", report, StringComparison.Ordinal);
            Assert.Contains("previews/page-02.jpg", report, StringComparison.Ordinal);
            Assert.Contains("story page(s) 2", string.Join('\n', result.Warnings), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>1792 x 2400 art with a drawn figure from y=1300 down to y=2000: the square crop ends at 1818, so it runs across the cut.</summary>
    private static byte[] CharacterAcrossTheCut()
    {
        using var bitmap = new SKBitmap(1792, 2400);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(235, 235, 235));
            using var dark = new SKPaint { Color = new SKColor(40, 40, 40) };
            using var light = new SKPaint { Color = new SKColor(160, 160, 160) };
            for (var y = 1300; y < 2000; y += 8)
            {
                for (var x = 600; x < 1200; x += 8)
                {
                    canvas.DrawRect(x, y, 8, 8, ((x / 8) + (y / 8)) % 2 == 0 ? dark : light);
                }
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }
}
