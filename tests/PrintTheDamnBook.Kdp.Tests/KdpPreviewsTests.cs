using PrintTheDamnBook.Cli;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Kdp;
using PrintTheDamnBook.Kdp.Previews;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp.Tests;

public class KdpPreviewsTests
{
    [Fact]
    public void The_previews_option_is_read_and_defaults_to_off()
    {
        Assert.False(KdpCommandLine.Parse(["a.dino", "--out", "o"]).Request!.Previews);
        Assert.True(KdpCommandLine.Parse(["a.dino", "--previews", "--out", "o"]).Request!.Previews);
    }

    [Fact]
    public void Previews_write_a_small_page_png_per_page_and_a_contact_sheet_inside_the_limit()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");

            var result = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, Previews: true));

            Assert.Equal(Path.Combine(outFolder, KdpPreviews.ContactSheetName), result.ContactSheetPath);
            for (var page = 1; page <= result.PageCount; page++)
            {
                using var png = SKBitmap.Decode(Path.Combine(outFolder, PagePath(page)));
                Assert.NotNull(png);
                Assert.True(png.Width < 600);
            }

            Assert.False(File.Exists(Path.Combine(outFolder, PagePath(result.PageCount + 1))));
            using var sheet = SKBitmap.Decode(result.ContactSheetPath);
            Assert.NotNull(sheet);
            Assert.Equal(KdpContactSheetLayout.For(result.PageCount).SheetWidth, sheet.Width);
            Assert.True(sheet.Width <= KdpContactSheetLayout.MaxSheetWidthPx);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static string PagePath(int position) => string.Format(System.Globalization.CultureInfo.InvariantCulture, KdpPreviews.PagePathFormat, position);

    [Fact]
    public void Without_the_option_no_preview_files_are_written_and_those_of_an_earlier_run_are_removed()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");
            KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder, Previews: true));
            Assert.True(File.Exists(Path.Combine(outFolder, PagePath(1))));

            var result = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder));

            Assert.Null(result.ContactSheetPath);
            Assert.Null(result.PreviewError);
            Assert.False(File.Exists(Path.Combine(outFolder, KdpPreviews.ContactSheetName)));
            Assert.False(Directory.Exists(Path.Combine(outFolder, KdpPreviews.PagesFolderName)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
