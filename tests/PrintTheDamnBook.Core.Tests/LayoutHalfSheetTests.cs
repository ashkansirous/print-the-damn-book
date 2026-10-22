using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests;

public class LayoutHalfSheetTests
{
    private static readonly SizeD Sample = new(896, 1200);

    [Theory]
    [InlineData(PaperKind.A4)]
    [InlineData(PaperKind.Letter)]
    public void Fit_KeepsAspectCentresAndStaysInsideMargin(PaperKind kind)
    {
        var half = HalfSheetLayout.HalfOf(Paper.LandscapeSheetMm(kind));
        var rect = HalfSheetLayout.Fit(Sample, half, HalfSheetLayout.DefaultMarginMm);

        Assert.Equal(Sample.Width / Sample.Height, rect.Width / rect.Height, 6);
        Assert.True(rect.X >= HalfSheetLayout.DefaultMarginMm - 1e-9);
        Assert.True(rect.Y >= HalfSheetLayout.DefaultMarginMm - 1e-9);
        Assert.True(rect.X + rect.Width <= half.Width - HalfSheetLayout.DefaultMarginMm + 1e-9);
        Assert.True(rect.Y + rect.Height <= half.Height - HalfSheetLayout.DefaultMarginMm + 1e-9);
        Assert.Equal(half.Width / 2, rect.X + rect.Width / 2, 6);
        Assert.Equal(half.Height / 2, rect.Y + rect.Height / 2, 6);
    }

    [Fact]
    public void Fit_A4PortraitPage_IsLimitedByWidth()
    {
        var half = HalfSheetLayout.HalfOf(Paper.LandscapeSheetMm(PaperKind.A4));
        var rect = HalfSheetLayout.Fit(Sample, half, 5);

        Assert.Equal(138.5, rect.Width, 6);
        Assert.Equal(138.5 * 1200 / 896, rect.Height, 6);
        Assert.Equal(5, rect.X, 6);
    }

    [Fact]
    public void Fit_LetterPortraitPage_IsLimitedByWidth()
    {
        var half = HalfSheetLayout.HalfOf(Paper.LandscapeSheetMm(PaperKind.Letter));
        var rect = HalfSheetLayout.Fit(Sample, half, 5);

        Assert.Equal(11 * 25.4 / 2 - 10, rect.Width, 6);
    }

    [Fact]
    public void Fit_WideImage_IsLimitedByWidthAndCentredVertically()
    {
        var rect = HalfSheetLayout.Fit(new SizeD(2000, 1000), new SizeD(100, 100), 10);

        Assert.Equal(new RectD(10, 30, 80, 40), rect);
    }

    [Fact]
    public void Fit_LargerMargin_ShrinksThePage()
    {
        var half = new SizeD(100, 150);

        var small = HalfSheetLayout.Fit(new SizeD(100, 150), half, 5);
        var large = HalfSheetLayout.Fit(new SizeD(100, 150), half, 15);

        Assert.True(large.Width < small.Width);
    }

    [Fact]
    public void Place_RightHalf_IsOffsetByHalfTheSheetWidth()
    {
        var sheet = Paper.LandscapeSheetMm(PaperKind.A4);

        var left = HalfSheetLayout.Place(Sample, sheet, rightHalf: false, margin: 5);
        var right = HalfSheetLayout.Place(Sample, sheet, rightHalf: true, margin: 5);

        Assert.Equal(left.X + sheet.Width / 2, right.X, 6);
        Assert.Equal(left.Y, right.Y);
        Assert.Equal(left.Width, right.Width);
    }

    [Theory]
    [InlineData(0, 10, 5)]
    [InlineData(10, -1, 5)]
    [InlineData(10, 10, 10)]
    [InlineData(10, 10, -1)]
    public void Fit_InvalidInput_Throws(double imageWidth, double imageHeight, double margin)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HalfSheetLayout.Fit(new SizeD(imageWidth, imageHeight), new SizeD(20, 20), margin));
    }

    [Fact]
    public void LandscapeSheet_IsWiderThanTall()
    {
        foreach (var kind in Enum.GetValues<PaperKind>())
        {
            var sheet = Paper.LandscapeSheetMm(kind);
            Assert.True(sheet.Width > sheet.Height);
        }
    }

    [Theory]
    [InlineData(PaperKind.A4, 210, 297, true)]
    [InlineData(PaperKind.A4, 297, 210, true)]
    [InlineData(PaperKind.A4, 209.6, 296.8, true)]
    [InlineData(PaperKind.A4, 215.9, 279.4, false)]
    [InlineData(PaperKind.Letter, 215.9, 279.4, true)]
    [InlineData(PaperKind.Letter, 210, 297, false)]
    [InlineData(PaperKind.A4, 235, 321, false)]
    public void MatchesSize_ComparesDimensionsWithinTolerance(PaperKind kind, double width, double height, bool expected)
    {
        Assert.Equal(expected, Paper.MatchesSize(kind, new SizeD(width, height), 2));
    }
}
