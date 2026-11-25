using PrintTheDamnBook.Cli;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Kdp.Styling;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>Slice 46 decision 9: <c>--speaker</c>, <c>--main-character</c> and <c>--title-picture</c> are read, checked and saved to <c>kdp.json</c> <c>styles</c>; a refused choice changes nothing.</summary>
public class KdpStyleChoicesTests
{
    private static readonly string[] Cast = ["Seagull", "Kathryn", "Izzy"];
    private const string PageText = """\"Squawk, look!\" said Seagull. \"Ahoy!\" said Kathryn.""";

    private static KdpExportRequest Request(string dino, string outFolder, KdpStyleChanges? styles) => new(dino, "en", outFolder, Styles: styles);

    private static string Dino(string folder) => SyntheticBook.WriteDino(Path.Combine(folder, "t.dino"), 3, pageText: PageText, cast: Cast);

    [Fact]
    public void A_speaker_a_main_character_and_a_title_picture_are_read()
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--speaker", "Seagull=#2f6b34", "--speaker", "Pancake Polly=#8A4B12", "--main-character", "Seagull=she speaks most", "--title-picture", "3=0.5,0.4,0.6"]);

        Assert.Null(parsed.Error);
        var styles = parsed.Request!.Styles!;
        Assert.Equal("#2F6B34", styles.Speakers!["Seagull"]);
        Assert.Equal("#8A4B12", styles.Speakers["Pancake Polly"]);
        Assert.Equal(new KdpMainCharacterChange("Seagull", "she speaks most"), styles.MainCharacter);
        Assert.Equal(new KdpTitlePictureChange(new KdpTitlePicture(3, 0.5, 0.4, 0.6)), styles.TitlePicture);
    }

    [Fact]
    public void Clear_removes_each_style_choice()
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--speaker", "Seagull=clear", "--main-character", "clear", "--title-picture", "clear"]);

        Assert.Null(parsed.Error);
        Assert.Null(parsed.Request!.Styles!.Speakers!["Seagull"]);
        Assert.Equal(new KdpMainCharacterChange(null), parsed.Request.Styles.MainCharacter);
        Assert.Equal(new KdpTitlePictureChange(null), parsed.Request.Styles.TitlePicture);
    }

    [Fact]
    public void A_request_without_style_options_has_no_styles()
    {
        Assert.Null(KdpCommandLine.Parse(["a.dino", "--out", "o"]).Request!.Styles);
    }

    [Theory]
    [InlineData("Seagull=#9CB4E0", "too light")]
    [InlineData("Seagull=#A31515", "red")]
    [InlineData("Seagull=#1F5FA8", "too close")]
    [InlineData("Seagull=blue", "not a colour")]
    [InlineData("Seagull", "not Seagull")]
    public void A_speaker_colour_that_fails_the_rules_is_refused_saying_which(string value, string reason)
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--speaker", value]);

        Assert.Null(parsed.Request);
        Assert.Contains(reason, parsed.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0=0.5,0.5,0.5")]
    [InlineData("3=1.5,0.5,0.5")]
    [InlineData("3=0.5,0.5,0")]
    [InlineData("3=0.5,0.5")]
    [InlineData("three=0.5,0.5,0.5")]
    public void A_bad_title_picture_is_refused(string value)
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--title-picture", value]);

        Assert.Null(parsed.Request);
        Assert.Contains("--title-picture needs", parsed.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_choices_are_saved_to_kdp_json_and_used_by_the_next_run()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = Dino(folder);
            var outFolder = Path.Combine(folder, "out");
            var styles = new KdpStyleChanges(new Dictionary<string, string?> { ["Seagull"] = "#2F6B34", ["Kathryn"] = "#8A4B12" }, new KdpMainCharacterChange("Seagull", "most lines"), new KdpTitlePictureChange(new KdpTitlePicture(2, 0.5, 0.5, 0.8)));

            var first = KdpExporter.Export(Request(dino, outFolder, styles));
            var saved = KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName));
            var second = KdpExporter.Export(Request(dino, outFolder, null));

            Assert.DoesNotContain(first.Warnings, w => w.StartsWith("styles:", StringComparison.Ordinal));
            Assert.Equal("#2F6B34", saved.Styles.SpeakerColours["Seagull"]);
            Assert.Equal(new KdpMainCharacter("Seagull", "most lines"), saved.Styles.MainCharacter);
            Assert.Equal(new KdpTitlePicture(2, 0.5, 0.5, 0.8), saved.Styles.TitlePicture);
            Assert.DoesNotContain(second.Warnings, w => w.StartsWith("styles:", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Without_saved_choices_the_report_names_each_fallback()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var result = KdpExporter.Export(Request(Dino(folder), Path.Combine(folder, "out"), null));

            var report = File.ReadAllText(result.ReportPath);
            Assert.Contains("no saved colour for Seagull, Kathryn (fallback palette used)", report, StringComparison.Ordinal);
            Assert.Contains("no saved main character (fallback)", report, StringComparison.Ordinal);
            Assert.Empty(result.Errors ?? []);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_colour_too_close_to_another_speakers_is_refused_before_anything_is_written()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var outFolder = Path.Combine(folder, "out");
            var styles = new KdpStyleChanges(new Dictionary<string, string?> { ["Seagull"] = "#2F6B34", ["Kathryn"] = "#2F6B36" });

            var ex = Assert.Throws<KdpException>(() => KdpExporter.Export(Request(Dino(folder), outFolder, styles)));

            Assert.Contains("--speaker Kathryn=#2F6B36 is refused", ex.Message, StringComparison.Ordinal);
            Assert.Contains("too close", ex.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(outFolder, KdpBookSettings.FileName)));
            Assert.False(File.Exists(Path.Combine(outFolder, KdpExporter.PdfName)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_speaker_or_main_character_who_is_not_in_the_cast_and_a_missing_title_page_are_refused()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var dino = Dino(folder);
            var outFolder = Path.Combine(folder, "out");

            var speaker = Assert.Throws<KdpException>(() => KdpExporter.Export(Request(dino, outFolder, new KdpStyleChanges(new Dictionary<string, string?> { ["Pancake Polly"] = "#2F6B34" }))));
            var main = Assert.Throws<KdpException>(() => KdpExporter.Export(Request(dino, outFolder, new KdpStyleChanges(MainCharacter: new KdpMainCharacterChange("Nobody")))));
            var title = Assert.Throws<KdpException>(() => KdpExporter.Export(Request(dino, outFolder, new KdpStyleChanges(TitlePicture: new KdpTitlePictureChange(new KdpTitlePicture(9, 0.5, 0.5, 0.5))))));

            Assert.Contains("not in the book's cast", speaker.Message, StringComparison.Ordinal);
            Assert.Contains("not in the book's cast", main.Message, StringComparison.Ordinal);
            Assert.Contains("has 3 story pages", title.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(outFolder, KdpBookSettings.FileName)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_kdp_json_that_cannot_be_read_is_left_as_it_is_when_styles_are_given()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            var path = Path.Combine(outFolder, KdpBookSettings.FileName);
            File.WriteAllText(path, "{ not json");
            var styles = new KdpStyleChanges(new Dictionary<string, string?> { ["Seagull"] = "#2F6B34" });

            Assert.Throws<KdpException>(() => KdpExporter.Export(Request(Dino(folder), outFolder, styles)));

            Assert.Equal("{ not json", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void An_older_kdp_json_without_styles_still_reads_and_keeps_its_offsets_when_styles_are_added()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            var path = Path.Combine(outFolder, KdpBookSettings.FileName);
            File.WriteAllText(path, """{ "pages": { "2": { "cropOffsetPx": 40 } } }""");

            KdpExporter.Export(Request(Dino(folder), outFolder, new KdpStyleChanges(new Dictionary<string, string?> { ["Seagull"] = "#2F6B34" })));

            var saved = KdpBookSettings.Read(path);
            Assert.Equal("#2F6B34", saved.Styles.SpeakerColours["Seagull"]);
            Assert.Equal([2], saved.Pages);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_speaker_colour_saved_for_a_name_that_left_the_cast_can_still_be_cleared()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            var path = Path.Combine(outFolder, KdpBookSettings.FileName);
            File.WriteAllText(path, """{ "styles": { "speakers": { "Ghost": "#2F6B34" } } }""");

            KdpExporter.Export(Request(Dino(folder), outFolder, new KdpStyleChanges(new Dictionary<string, string?> { ["Ghost"] = null })));

            Assert.False(KdpBookSettings.Read(path).Styles.SpeakerColours.ContainsKey("Ghost"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_saved_main_character_who_never_speaks_gets_a_report_warning()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var styles = new KdpStyleChanges(MainCharacter: new KdpMainCharacterChange("Izzy", "the hero"));

            var result = KdpExporter.Export(Request(Dino(folder), Path.Combine(folder, "out"), styles));

            Assert.Contains(result.Warnings, w => w.Contains("\"Izzy\" has no quoted lines", StringComparison.Ordinal));
            Assert.Contains("\"Izzy\" has no quoted lines", File.ReadAllText(result.ReportPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("Clear")]
    [InlineData("CLEAR")]
    public void Clear_is_matched_whatever_its_case(string word)
    {
        var parsed = KdpCommandLine.Parse(["a.dino", "--out", "o", "--speaker", $"Seagull={word}"]);

        Assert.Null(parsed.Error);
        Assert.Null(parsed.Request!.Styles!.Speakers!["Seagull"]);
    }

    [Fact]
    public void A_speaker_in_the_cast_who_is_not_a_detected_speaker_is_saved_with_a_report_warning_naming_it()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var outFolder = Path.Combine(folder, "out");

            var result = KdpExporter.Export(Request(Dino(folder), outFolder, new KdpStyleChanges(new Dictionary<string, string?> { ["Izzy"] = "#2F6B34" })));

            Assert.Contains(result.Warnings, w => w.Contains("--speaker Izzy=#2F6B34", StringComparison.Ordinal) && w.Contains("not a speaking character", StringComparison.Ordinal));
            Assert.Equal("#2F6B34", KdpBookSettings.Read(Path.Combine(outFolder, KdpBookSettings.FileName)).Styles.SpeakerColours["Izzy"]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Kdp_json_is_written_after_the_pdfs_report_previews_crop_sheet_and_style_sheet()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var outFolder = Path.Combine(folder, "out");
            var request = new KdpExportRequest(Dino(folder), "en", outFolder, Previews: true, CropSheet: true, StyleSheet: true, Styles: new KdpStyleChanges(new Dictionary<string, string?> { ["Seagull"] = "#2F6B34" }));

            var result = KdpExporter.Export(request);

            var saved = File.GetLastWriteTimeUtc(Path.Combine(outFolder, KdpBookSettings.FileName));
            foreach (var other in new[] { result.PdfPath, result.ReportPath, result.ContactSheetPath!, result.CropSheetPath!, result.StyleSheetPath! })
            {
                Assert.True(saved >= File.GetLastWriteTimeUtc(other), $"kdp.json was written before {other}");
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_crop_sheet_that_only_the_style_sheet_asked_for_is_a_warning_not_an_error()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            File.WriteAllText(Path.Combine(outFolder, "crop"), "a file where the folder should go");

            var result = KdpExporter.Export(new KdpExportRequest(Dino(folder), "en", outFolder, StyleSheet: true));
            var asked = KdpExporter.Export(new KdpExportRequest(Dino(folder), "en", outFolder, CropSheet: true));

            Assert.Null(result.CropSheetError);
            Assert.Contains(result.Warnings, w => w.Contains("crop sheet", StringComparison.Ordinal));
            Assert.NotNull(asked.CropSheetError);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_style_sheet_that_cannot_be_written_is_reported_and_never_stops_the_other_files()
    {
        var folder = SyntheticBook.NewFolder();
        try
        {
            var outFolder = Path.Combine(folder, "out");
            Directory.CreateDirectory(outFolder);
            File.WriteAllText(Path.Combine(outFolder, "styles"), "a file where the folder should go");

            var result = KdpExporter.Export(new KdpExportRequest(Dino(folder), "en", outFolder, StyleSheet: true));

            Assert.NotNull(result.StyleSheetError);
            Assert.Null(result.StyleSheetPath);
            Assert.True(File.Exists(result.PdfPath));
            Assert.True(File.Exists(result.ReportPath));
            Assert.Empty(result.Errors ?? []);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
