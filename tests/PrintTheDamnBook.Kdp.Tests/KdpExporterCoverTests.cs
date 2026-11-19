using PrintTheDamnBook.Cli;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Kdp;

namespace PrintTheDamnBook.Kdp.Tests;

public class KdpExporterCoverTests
{
    private const string TooLongForTheCover = "Supercalifragilisticexpialidocious Pneumonoultramicroscopicsilicovolcanoconiosis Hippopotomonstrosesquippedaliophobia Floccinaucinihilipilification Antidisestablishmentarianism Honorificabilitudinitatibus";

    [Fact]
    public void Export_writes_cover_pdf_and_a_cover_section_in_the_report()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");

            var result = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder));

            Assert.Null(result.CoverError);
            Assert.True(File.Exists(Path.Combine(outFolder, KdpExporter.CoverName)));
            Assert.Contains("## Cover", File.ReadAllText(result.ReportPath), StringComparison.Ordinal);
            Assert.Contains(KdpExporter.NoCoverWarning, result.Warnings);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_cover_failure_with_no_older_cover_does_not_claim_one_was_removed()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3, title: TooLongForTheCover);
            var outFolder = Path.Combine(folder, "out");

            var result = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder));

            Assert.NotNull(result.CoverError);
            Assert.DoesNotContain("older cover.pdf", File.ReadAllText(result.ReportPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_cover_failure_still_writes_interior_and_report_and_the_command_exits_1()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3, title: TooLongForTheCover);
            var outFolder = Path.Combine(folder, "out");
            var error = new StringWriter();
            Directory.CreateDirectory(outFolder);
            File.WriteAllText(Path.Combine(outFolder, KdpExporter.CoverName), "stale");

            var code = KdpRunner.Run(["kdp", dino, "--out", outFolder, "--offset", "2=40"], new StringWriter(), error);

            Assert.Equal(KdpRunner.Failed, code);
            Assert.Contains("cover.pdf was not written", error.ToString(), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(outFolder, KdpExporter.PdfName)));
            Assert.False(File.Exists(Path.Combine(outFolder, KdpExporter.CoverName)));
            Assert.Equal("stale", File.ReadAllText(Path.Combine(outFolder, KdpExporter.PreviousCoverName)));
            Assert.Contains("kept as cover-previous.pdf", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("cover.pdf was not written", File.ReadAllText(Path.Combine(outFolder, KdpExporter.ReportName)), StringComparison.Ordinal);
            Assert.Equal(40, KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName)).OffsetFor(2));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_non_kdp_exception_in_the_cover_pipeline_still_writes_interior_and_report_and_keeps_the_older_cover()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3, corruptBackCover: true);
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            var older = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(Path.Combine(outFolder, KdpExporter.CoverName), older);
            File.WriteAllText(Path.Combine(outFolder, KdpExporter.PreviousCoverName), "even older");

            var result = KdpExporter.Export(new KdpExportRequest(dino, "en", outFolder));

            Assert.NotNull(result.CoverError);
            Assert.True(File.Exists(Path.Combine(outFolder, KdpExporter.PdfName)));
            Assert.Contains("cover.pdf was not written", File.ReadAllText(result.ReportPath), StringComparison.Ordinal);
            Assert.Contains("kept as cover-previous.pdf", File.ReadAllText(result.ReportPath), StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(outFolder, KdpExporter.CoverName)));
            Assert.Equal(older, File.ReadAllBytes(Path.Combine(outFolder, KdpExporter.PreviousCoverName)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
