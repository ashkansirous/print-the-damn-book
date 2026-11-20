using PrintTheDamnBook.Cli;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Kdp;
using SkiaSharp;
using UglyToad.PdfPig;

namespace PrintTheDamnBook.Kdp.Tests;

public class KdpCommandLineTests
{
    [Fact]
    public void The_book_language_and_folder_are_read()
    {
        var parsed = KdpCommandLine.Parse(["freddy.dino", "--language", "fa", "--out", "build"]);

        Assert.Null(parsed.Error);
        Assert.Equal(new KdpExportRequest("freddy.dino", "fa", "build"), parsed.Request);
    }

    [Fact]
    public void A_text_choice_is_read_as_a_box_and_two_colours()
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--text", "front=0.1,0.55,0.8,0.2,#ffffff,102030", "--text", "back=0.3,0.05,0.6,0.25,#000000,#FFFFFF"]);

        Assert.Null(parsed.Error);
        var texts = parsed.Request!.CoverText!;
        Assert.Equal(new KdpCoverText(new KdpTextBox(0.1, 0.55, 0.8, 0.2), "#FFFFFF", "#102030"), texts["front"]);
        Assert.Equal(new KdpCoverText(new KdpTextBox(0.3, 0.05, 0.6, 0.25), "#000000", "#FFFFFF"), texts["back"]);
    }

    [Fact]
    public void Clear_removes_a_text_choice_and_the_dino_colours()
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--text", "front=clear", "--dino-text", "back=clear"]);

        Assert.Null(parsed.Error);
        Assert.True(parsed.Request!.CoverText!.ContainsKey("front"));
        Assert.Null(parsed.Request.CoverText["front"]);
        Assert.Equal(new KdpDinoTextChange(null), parsed.Request.DinoText);
    }

    [Fact]
    public void The_dino_colours_are_read()
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--dino-text", "back=#101010,#fafafa"]);

        Assert.Equal(new KdpDinoTextChange(new KdpTextColors("#101010", "#FAFAFA")), parsed.Request!.DinoText);
    }

    [Theory]
    [InlineData("--text", "spine=0.1,0.1,0.5,0.2,#FFFFFF,#000000")]
    [InlineData("--text", "front=0.1,0.1,0.5,0.2,#FFFFFF")]
    [InlineData("--text", "front=0.1,0.1,0.5,0.2,white,#000000")]
    [InlineData("--text", "front=0.6,0.1,0.5,0.2,#FFFFFF,#000000")]
    [InlineData("--text", "front=a,0.1,0.5,0.2,#FFFFFF,#000000")]
    [InlineData("--dino-text", "front=#FFFFFF,#000000")]
    [InlineData("--dino-text", "back=#FFFFFF")]
    [InlineData("--dino-text", "back=#FFFFFF,nope")]
    public void A_wrong_text_choice_is_refused_with_the_usage(string option, string value)
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", option, value]);

        Assert.Null(parsed.Request);
        Assert.NotNull(parsed.Error);
    }

    [Fact]
    public void A_text_option_without_a_value_is_refused()
    {
        Assert.NotNull(KdpCommandLine.Parse(["a.dino", "--out", "o", "--text"]).Error);
        Assert.NotNull(KdpCommandLine.Parse(["a.dino", "--out", "o", "--dino-text", "--previews"]).Error);
    }

    [Fact]
    public void The_language_defaults_to_english()
    {
        Assert.Equal("en", KdpCommandLine.Parse(["a.dino", "--out", "o"]).Request!.Language);
    }

    [Theory]
    [InlineData("--out", "o")]
    [InlineData("a.dino")]
    [InlineData("a.dino", "--out")]
    [InlineData("a.dino", "--out", "--language", "en")]
    [InlineData("a.dino", "--out", "o", "--language", "--out")]
    [InlineData("a.dino", "--out", "o", "--wat")]
    [InlineData("a.dino", "b.dino", "--out", "o")]
    public void Wrong_arguments_are_named(params string[] args)
    {
        var parsed = KdpCommandLine.Parse(args);

        Assert.Null(parsed.Request);
        Assert.False(string.IsNullOrEmpty(parsed.Error));
    }

    [Fact]
    public void No_command_is_exit_code_2_and_prints_the_usage()
    {
        var error = new StringWriter();

        var code = KdpRunner.Run([], new StringWriter(), error);

        Assert.Equal(KdpRunner.BadArguments, code);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_book_is_exit_code_1_with_a_message()
    {
        var folder = SyntheticBook.NewFolder();
        var error = new StringWriter();
        try
        {
            var code = KdpRunner.Run(["kdp", Path.Combine(folder, "none.dino"), "--out", folder], new StringWriter(), error);

            Assert.Equal(KdpRunner.Failed, code);
            Assert.Contains("Error:", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_language_the_book_lacks_is_exit_code_1()
    {
        var folder = SyntheticBook.NewFolder();
        var error = new StringWriter();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 2);

            var code = KdpRunner.Run(["kdp", dino, "--language", "de", "--out", folder], new StringWriter(), error);

            Assert.Equal(KdpRunner.Failed, code);
            Assert.Contains("no \"de\" text", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_page_skipped_on_reading_is_a_warning_in_the_report_and_on_stderr_not_a_failure()
    {
        var folder = SyntheticBook.NewFolder();
        var error = new StringWriter();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 4, withoutPicture: 1);
            var outFolder = Path.Combine(folder, "out");

            var code = KdpRunner.Run(["kdp", dino, "--out", outFolder], new StringWriter(), error);

            Assert.Equal(KdpRunner.Ok, code);
            Assert.Contains("Building t.dino ...", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("1 page(s) of the book were skipped", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("1 page(s) of the book were skipped", File.ReadAllText(Path.Combine(outFolder, KdpExporter.ReportName)), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_picture_over_the_pixel_cap_is_refused_before_it_is_decoded()
    {
        using var bitmap = new SKBitmap(7_200, 7_200, SKColorType.Gray8, SKAlphaType.Opaque);
        using var image = SKImage.FromBitmap(bitmap);
        var png = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();

        var ex = Assert.Throws<KdpException>(() => KdpImageDecoder.Prepare(png));

        Assert.Contains("pixel limit", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dino_book_becomes_the_pdf_and_the_report_with_exit_code_0()
    {
        var folder = SyntheticBook.NewFolder();
        var output = new StringWriter();
        try
        {
            var dino = SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3);
            var outFolder = Path.Combine(folder, "out");

            var code = KdpRunner.Run(["kdp", dino, "--language", "en", "--out", outFolder], output, new StringWriter());

            Assert.Equal(KdpRunner.Ok, code);
            using var pdf = PdfDocument.Open(File.ReadAllBytes(Path.Combine(outFolder, KdpExporter.PdfName)));
            Assert.Equal(24, pdf.NumberOfPages);
            Assert.Contains("Pages: 24", File.ReadAllText(Path.Combine(outFolder, KdpExporter.ReportName)), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
