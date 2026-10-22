using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests;

public class LayoutPageNumberTests
{
    private const double Margin = 5;
    private static readonly SizeD Tall = new(100, 200);

    public static TheoryData<PaperKind> Papers => new() { PaperKind.A4, PaperKind.Letter };

    [Fact]
    public void Off_leaves_the_placement_unchanged()
    {
        var sheet = Paper.LandscapeSheetMm(PaperKind.A4);

        var off = HalfSheetLayout.Resolve(Tall, sheet, false, Margin, ScalingMode.Fit, PageNumberPosition.Off);

        Assert.Equal(HalfSheetLayout.Resolve(Tall, sheet, false, Margin, ScalingMode.Fit), off);
        Assert.Null(HalfSheetLayout.PageNumberRect(sheet, false, Margin, PageNumberPosition.Off));
    }

    [Theory]
    [MemberData(nameof(Papers))]
    public void Below_keeps_the_fitted_image_clear_of_the_number_strip(PaperKind paper)
    {
        var sheet = Paper.LandscapeSheetMm(paper);
        var placement = HalfSheetLayout.Resolve(Tall, sheet, true, Margin, ScalingMode.Fit, PageNumberPosition.Below);
        var strip = HalfSheetLayout.PageNumberRect(sheet, true, Margin, PageNumberPosition.Below)!.Value;

        Assert.True(placement.Image.Y + placement.Image.Height <= strip.Y + 1e-9);
        Assert.Equal(sheet.Height - Margin, strip.Y + strip.Height, 6);
        Assert.Equal(sheet.Width / 2, strip.X, 6);
    }

    [Theory]
    [MemberData(nameof(Papers))]
    public void Above_keeps_the_fitted_image_clear_of_the_number_strip(PaperKind paper)
    {
        var sheet = Paper.LandscapeSheetMm(paper);
        var placement = HalfSheetLayout.Resolve(Tall, sheet, false, Margin, ScalingMode.Fit, PageNumberPosition.Above);
        var strip = HalfSheetLayout.PageNumberRect(sheet, false, Margin, PageNumberPosition.Above)!.Value;

        Assert.True(placement.Image.Y >= strip.Y + strip.Height - 1e-9);
        Assert.Equal(Margin, strip.Y, 6);
        Assert.Equal(0, strip.X, 6);
    }

    [Theory]
    [InlineData(PageNumberPosition.Below)]
    [InlineData(PageNumberPosition.Above)]
    public void Fill_visible_area_shrinks_to_leave_the_strip(PageNumberPosition position)
    {
        var sheet = Paper.LandscapeSheetMm(PaperKind.A4);
        var band = PageNumbering.BandMm(sheet);
        var plain = HalfSheetLayout.Resolve(new SizeD(100, 100), sheet, false, Margin, ScalingMode.Fill);

        var numbered = HalfSheetLayout.Resolve(new SizeD(100, 100), sheet, false, Margin, ScalingMode.Fill, position);
        var strip = HalfSheetLayout.PageNumberRect(sheet, false, Margin, position)!.Value;

        Assert.Equal(plain.Visible.Height - band, numbered.Visible.Height, 6);
        var overlaps = numbered.Visible.Y < strip.Y + strip.Height - 1e-9 && strip.Y < numbered.Visible.Y + numbered.Visible.Height - 1e-9;
        Assert.False(overlaps);
    }

    [Theory]
    [InlineData(PageNumberPosition.Below, 0)]
    [InlineData(PageNumberPosition.Below, 2)]
    [InlineData(PageNumberPosition.Above, 0)]
    [InlineData(PageNumberPosition.Above, 2)]
    [InlineData(PageNumberPosition.Below, 5)]
    [InlineData(PageNumberPosition.Above, 12)]
    public void Number_strip_keeps_a_minimum_distance_from_the_paper_edge(PageNumberPosition position, double margin)
    {
        var sheet = Paper.LandscapeSheetMm(PaperKind.A4);
        var strip = HalfSheetLayout.PageNumberRect(sheet, false, margin, position)!.Value;
        var placement = HalfSheetLayout.Resolve(Tall, sheet, false, margin, ScalingMode.Fit, position);

        var edgeDistance = position == PageNumberPosition.Below ? sheet.Height - (strip.Y + strip.Height) : strip.Y;

        Assert.True(edgeDistance >= PageNumbering.MinEdgeDistanceMm - 1e-9);
        Assert.True(edgeDistance >= margin - 1e-9);
        Assert.True(placement.Image.Y >= margin - 1e-9);
        Assert.True(placement.Image.Y + placement.Image.Height <= sheet.Height - margin + 1e-9);
    }

    [Theory]
    [InlineData(PageNumberPosition.Below)]
    [InlineData(PageNumberPosition.Above)]
    public void Strip_is_exactly_what_the_image_area_leaves(PageNumberPosition position)
    {
        var sheet = Paper.LandscapeSheetMm(PaperKind.A4);
        var half = HalfSheetLayout.HalfOf(sheet);
        var band = PageNumbering.BandMm(sheet);
        var area = HalfSheetLayout.Available(half, 0, position, band);
        var strip = HalfSheetLayout.PageNumberRect(sheet, false, 0, position)!.Value;

        var touching = position == PageNumberPosition.Below ? area.Y + area.Height : strip.Y + strip.Height;
        var other = position == PageNumberPosition.Below ? strip.Y : area.Y;
        Assert.Equal(other, touching, 6);
        Assert.Equal(band, strip.Height, 6);
    }

    [Fact]
    public void Number_strip_turns_with_the_sheet()
    {
        var sheet = Paper.LandscapeSheetMm(PaperKind.A4);
        var strip = HalfSheetLayout.PageNumberRect(sheet, false, Margin, PageNumberPosition.Below)!.Value;

        var turned = HalfSheetLayout.Rotate180(strip, sheet);

        Assert.Equal(sheet.Width / 2, turned.X, 6);
        Assert.Equal(sheet.Height - (strip.Y + strip.Height), turned.Y, 6);
    }

    [Fact]
    public void Margin_that_leaves_no_room_for_the_number_is_rejected()
    {
        var sheet = Paper.LandscapeSheetMm(PaperKind.A4);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HalfSheetLayout.Resolve(Tall, sheet, false, 100, ScalingMode.Fit, PageNumberPosition.Below));
    }

    [Fact]
    public void Missing_setting_reads_as_off_and_a_bad_value_is_repaired()
    {
        Assert.Equal(PageNumberPosition.Off, PrintSettingsJson.Parse("{}").PageNumbers);
        Assert.Equal(PageNumberPosition.Off, PrintSettingsJson.Parse("{\"PageNumbers\":99}").PageNumbers);
        var round = PrintSettingsJson.Parse(PrintSettingsJson.Serialize(new PrintSettings { PageNumbers = PageNumberPosition.Above }));
        Assert.Equal(PageNumberPosition.Above, round.PageNumbers);
    }
}
