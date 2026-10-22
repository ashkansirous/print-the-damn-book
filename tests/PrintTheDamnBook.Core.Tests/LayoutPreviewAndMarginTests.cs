using System.Globalization;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests;

public sealed class LayoutPreviewAndMarginTests
{
    private static readonly CultureInfo CommaCulture = CultureInfo.GetCultureInfo("de-DE");

    [Fact]
    public void Preview_box_has_the_half_sheet_aspect_and_a_scaled_inset()
    {
        var box = HalfSheetLayout.Preview(new SizeD(297, 210), 5, 148.5);

        Assert.Equal(148.5, box.Width);
        Assert.Equal(210, box.Height, 6);
        Assert.Equal(5, box.Inset, 6);
    }

    [Fact]
    public void Preview_inset_follows_the_box_scale_and_clamps_the_margin()
    {
        var box = HalfSheetLayout.Preview(new SizeD(297, 210), 500, 74.25);

        Assert.Equal(12.5, box.Inset, 6);
    }

    public static TheoryData<string, string> Cultures => new()
    {
        { "en-GB", "3,5" }, { "en-GB", "3.5" }, { "", "3,5" }, { "", "3.5" }, { "de-DE", "3,5" }, { "de-DE", "3.5" },
    };

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Margin_text_accepts_either_decimal_separator_in_any_culture(string cultureName, string text)
    {
        using var _ = new CultureScope(cultureName);

        Assert.True(HalfSheetLayout.TryParseMargin(text, out var value));
        Assert.Equal(3.5, value);
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("")]
    public void Margin_text_with_two_separators_is_rejected(string cultureName)
    {
        using var scope = new CultureScope(cultureName);

        Assert.False(HalfSheetLayout.TryParseMargin("3,5,1", out _));
        Assert.False(HalfSheetLayout.TryParseMargin("3.5.1", out _));
    }

    [Fact]
    public void Margin_text_is_trimmed()
    {
        Assert.True(HalfSheetLayout.TryParseMargin(" 12 ", out var value));
        Assert.Equal(12, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData(null)]
    public void Margin_text_that_is_not_a_number_is_rejected(string? text)
    {
        Assert.False(HalfSheetLayout.TryParseMargin(text, out _));
    }

    [Fact]
    public void Formatted_margin_is_clamped_and_uses_the_culture()
    {
        Assert.Equal("25", HalfSheetLayout.FormatMargin(500, CommaCulture));
        Assert.Equal("5,5", HalfSheetLayout.FormatMargin(5.5, CommaCulture));
    }

    [Fact]
    public void A_failed_save_returns_false_and_leaves_no_temp_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ptdb-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            Directory.CreateDirectory(path);

            Assert.False(PrintSettingsJson.TrySave(path, new PrintSettings()));
            Assert.Empty(Directory.GetFiles(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previous;
        }
    }
}
