using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests;

public sealed class LayoutSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ptdb-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private string FilePath => Path.Combine(_dir, "sub", "settings.json");

    [Fact]
    public void Defaults_are_fit_short_edge_a4_and_five_millimetres()
    {
        var settings = new PrintSettings();

        Assert.Equal(ScalingMode.Fit, settings.Scaling);
        Assert.Equal(FlipEdge.ShortEdge, settings.Flip);
        Assert.Equal(PaperKind.A4, settings.Paper);
        Assert.Equal(5, settings.MarginMm);
        Assert.Null(settings.Printer);
    }

    [Fact]
    public void Missing_file_gives_the_defaults()
    {
        var settings = PrintSettingsJson.Load(FilePath);

        Assert.Equal(ScalingMode.Fit, settings.Scaling);
        Assert.Empty(settings.Printers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("{\"Scaling\":\"Sideways\"}")]
    [InlineData("{\"MarginMm\":\"wide\"}")]
    public void Corrupt_or_empty_content_gives_the_defaults(string json)
    {
        var settings = PrintSettingsJson.Parse(json);

        Assert.Equal(ScalingMode.Fit, settings.Scaling);
        Assert.Equal(5, settings.MarginMm);
        Assert.Empty(settings.Printers);
    }

    [Fact]
    public void Unreadable_file_content_on_disk_gives_the_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllBytes(FilePath, [0xFF, 0x00, 0xFE, 0x7B]);

        Assert.Equal(ScalingMode.Fit, PrintSettingsJson.Load(FilePath).Scaling);
    }

    [Fact]
    public void A_partial_file_keeps_what_it_has_and_defaults_the_rest()
    {
        var settings = PrintSettingsJson.Parse("{\"Scaling\":\"Fill\"}");

        Assert.Equal(ScalingMode.Fill, settings.Scaling);
        Assert.Equal(FlipEdge.ShortEdge, settings.Flip);
        Assert.Equal(5, settings.MarginMm);
    }

    [Fact]
    public void Out_of_range_values_are_repaired()
    {
        var settings = PrintSettingsJson.Parse("{\"Flip\":9,\"Paper\":-4,\"MarginMm\":900,\"Printers\":{\"A\":null,\"B\":{\"Duplex\":7}}}");

        Assert.Equal(FlipEdge.ShortEdge, settings.Flip);
        Assert.Equal(PaperKind.A4, settings.Paper);
        Assert.Equal(HalfSheetLayout.MaxMarginMm, settings.MarginMm);
        Assert.Empty(settings.Printers);
    }

    [Fact]
    public void Saved_settings_load_back_unchanged()
    {
        var settings = new PrintSettings { Scaling = ScalingMode.Fill, Flip = FlipEdge.LongEdge, MarginMm = 3.5, Paper = PaperKind.Letter, Printer = "Office" }
            .WithPrinter("Office", new PrinterSettings(DuplexMode.Manual, true, false));

        Assert.True(PrintSettingsJson.TrySave(FilePath, settings));
        var loaded = PrintSettingsJson.Load(FilePath);

        Assert.Equal(ScalingMode.Fill, loaded.Scaling);
        Assert.Equal(FlipEdge.LongEdge, loaded.Flip);
        Assert.Equal(3.5, loaded.MarginMm);
        Assert.Equal(PaperKind.Letter, loaded.Paper);
        Assert.Equal("Office", loaded.Printer);
        Assert.Equal(new PrinterSettings(DuplexMode.Manual, true, false), loaded.ForPrinter("office"));
    }

    [Fact]
    public void An_unknown_printer_gets_the_defaults()
    {
        var printer = new PrintSettings().ForPrinter("Nope");

        Assert.Equal(DuplexMode.Automatic, printer.Duplex);
        Assert.False(printer.ReverseBacks);
        Assert.Equal(ManualDuplexPlan.DefaultRotateBacks, printer.RotateBacks);
    }

    [Fact]
    public void Save_to_an_unwritable_location_reports_failure_instead_of_throwing()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");

        Assert.False(PrintSettingsJson.TrySave(Path.Combine(blocker, "settings.json"), new PrintSettings()));
    }

    [Fact]
    public void Separate_cover_job_is_remembered()
    {
        Assert.True(PrintSettingsJson.TrySave(FilePath, new PrintSettings { SeparateCoverJob = true }));

        Assert.True(PrintSettingsJson.Load(FilePath).SeparateCoverJob);
    }

    [Fact]
    public void Saving_to_an_empty_path_returns_false_instead_of_throwing()
    {
        Assert.False(PrintSettingsJson.TrySave("", new PrintSettings()));
    }
}
