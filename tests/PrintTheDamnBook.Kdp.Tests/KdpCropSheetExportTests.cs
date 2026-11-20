using PrintTheDamnBook.Cli;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Kdp;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>Slice 36 B and C: <c>--offset front=...</c> and <c>--crop-sheet</c> on the command line and through the export.</summary>
public class KdpCropSheetExportTests
{
    [Fact]
    public void Offsets_for_the_covers_and_the_crop_sheet_option_are_read()
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--offset", "3=40", "--offset", "front=120", "--offset", "back=0", "--crop-sheet", "--out", "o"]);

        Assert.Null(parsed.Error);
        var request = parsed.Request!;
        Assert.Equal(40, request.Offsets![3]);
        Assert.Equal(120, request.CoverOffsets![KdpPictureKeys.Front]);
        Assert.Equal(0, request.CoverOffsets[KdpPictureKeys.Back]);
        Assert.True(request.CropSheet);
    }

    [Fact]
    public void Without_the_option_there_is_no_crop_sheet_and_no_cover_offsets()
    {
        var request = KdpCommandLine.Parse(["a.dino", "--out", "o"]).Request!;

        Assert.False(request.CropSheet);
        Assert.Null(request.CoverOffsets);
    }

    [Theory]
    [InlineData("spine=5")]
    [InlineData("front=-5")]
    [InlineData("front")]
    [InlineData("0=5")]
    public void A_bad_offset_key_is_named(string offset)
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--offset", offset, "--out", "o"]);

        Assert.Null(parsed.Request);
        Assert.Contains("--offset", parsed.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_crop_sheet_has_one_whole_picture_per_story_page_and_the_front_and_the_larger_copies()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");

            var result = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, CropSheet: true));

            Assert.Null(result.CropSheetError);
            Assert.Equal(Path.Combine(outFolder, "crop", "sheet.png"), result.CropSheetPath);
            var layout = KdpCropSheetLayout.For(4);
            using var sheet = SKBitmap.Decode(result.CropSheetPath);
            Assert.Equal((layout.SheetWidth, layout.SheetHeight), (sheet.Width, sheet.Height));
            foreach (var key in new[] { "1", "2", "3", "front" })
            {
                using var detail = SKBitmap.Decode(Path.Combine(outFolder, KdpCropSheetLayout.DetailPath(key)));
                Assert.Equal((KdpCropSheetLayout.DetailWidthPx, 643 + KdpCropSheetLayout.DetailLabelHeightPx), (detail.Width, detail.Height));
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_larger_copy_outlines_the_window_in_use_so_an_offset_moves_the_outline()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 2);
            var outFolder = Path.Combine(folder, "out");
            KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, CropSheet: true));
            using var before = SKBitmap.Decode(Path.Combine(outFolder, KdpCropSheetLayout.DetailPath("1")));

            KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, Offsets: new Dictionary<int, int> { [1] = 300 }, CropSheet: true));
            using var after = SKBitmap.Decode(Path.Combine(outFolder, KdpCropSheetLayout.DetailPath("1")));

            var top = KdpCropSheetLayout.DetailLabelHeightPx;
            Assert.True(IsRed(before.GetPixel(240, top)));
            Assert.False(IsRed(after.GetPixel(240, top)));
            Assert.True(IsRed(after.GetPixel(240, top + 80)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_crop_sheet_lists_only_story_pages_and_shows_the_offset_the_pdf_uses()
    {
        var pictures = new BookPictures([SyntheticBook.Picture(1), SyntheticBook.Picture(2), SyntheticBook.Picture(3)], null, null);
        var settings = KdpBookSettings.Empty.WithOffset(1, 100000);

        var listed = KdpExporter.CropSheetPictures(pictures, settings, storyPages: 2);

        Assert.Equal(["1", "2", KdpPictureKeys.Front], listed.Select(p => p.Key));
        using var decoded = SKBitmap.Decode(listed[0].Encoded);
        var outline = KdpCropOutline.For(decoded.Width, decoded.Height, listed[0].OffsetPx, 200, 267);
        Assert.Equal(outline.MaxOffsetPx, outline.AppliedOffsetPx);
        Assert.Equal(KdpPictureCrop.For(decoded.Width, decoded.Height, 100000).Source.Y, outline.AppliedOffsetPx);
    }

    [Fact]
    public void A_cover_offset_is_warned_about_when_the_cover_cannot_be_made_and_is_not_saved()
    {
        var request = new KdpExportRequest("a.dino", "en", "out", CoverOffsets: new Dictionary<string, int> { [KdpPictureKeys.Front] = 380, [KdpPictureKeys.Back] = 0 });

        var warnings = KdpExporter.UnsavedCoverOffsetWarnings(request, cover: null).ToList();

        var warning = Assert.Single(warnings);
        Assert.Contains("--offset front=380 was not saved", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_without_the_option_removes_the_crop_files_of_an_earlier_run()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 2);
            var outFolder = Path.Combine(folder, "out");
            KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, CropSheet: true));
            Assert.True(File.Exists(Path.Combine(outFolder, "crop", "sheet.png")));

            var result = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder));

            Assert.Null(result.CropSheetPath);
            Assert.False(Directory.Exists(Path.Combine(outFolder, "crop")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_command_prints_where_the_crop_sheet_is()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 2);
            var output = new StringWriter();

            var code = KdpRunner.Run(["kdp", dino, "--crop-sheet", "--out", Path.Combine(folder, "out")], output, new StringWriter());

            Assert.Equal(KdpRunner.Ok, code);
            Assert.Contains("crop sheet", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The outline is drawn pure red.</summary>
    private static bool IsRed(SKColor c) => c.Red > 200 && c.Green < 60 && c.Blue < 60;
}
