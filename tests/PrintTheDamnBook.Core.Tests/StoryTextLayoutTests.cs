using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests;

public class StoryTextLayoutTests
{
    private const double Tolerance = 1e-9;

    private static SizeD Sheet(PaperKind paper) => Paper.LandscapeSheetMm(paper);

    private static RectD Visible(PaperKind paper, bool right, ScalingMode mode, PageNumberPosition numbers)
    {
        var picture = new SizeD(1493, 2000);
        return HalfSheetLayout.Resolve(picture, Sheet(paper), right, HalfSheetLayout.DefaultMarginMm, mode, numbers).Visible;
    }

    [Theory]
    [InlineData(PaperKind.A4, ScalingMode.Fit, PageNumberPosition.Off)]
    [InlineData(PaperKind.A4, ScalingMode.Fit, PageNumberPosition.Below)]
    [InlineData(PaperKind.A4, ScalingMode.Fill, PageNumberPosition.Above)]
    [InlineData(PaperKind.Letter, ScalingMode.Fit, PageNumberPosition.Below)]
    [InlineData(PaperKind.Letter, ScalingMode.Fill, PageNumberPosition.Off)]
    public void Band_IsAtMostThirtyPercentOfThePictureAndSitsInsideItsBottom(PaperKind paper, ScalingMode mode, PageNumberPosition numbers)
    {
        var visible = Visible(paper, true, mode, numbers);

        var band = StoryTextLayout.Band(visible);

        Assert.True(band.MaxPanel.Height + (visible.Y + visible.Height - band.MaxPanel.Y - band.MaxPanel.Height) <= 0.30 * visible.Height + Tolerance);
        Assert.True(band.MaxPanel.X > visible.X);
        Assert.True(band.MaxPanel.X + band.MaxPanel.Width < visible.X + visible.Width);
        var bottomGap = visible.Y + visible.Height - (band.MaxPanel.Y + band.MaxPanel.Height);
        Assert.True(bottomGap > 0);
        Assert.True(band.MaxPanel.Y >= visible.Y + 0.70 * visible.Height - Tolerance);
    }

    [Fact]
    public void Band_UsesDinoReaderProportions()
    {
        var visible = new RectD(0, 0, 360, 480);

        var band = StoryTextLayout.Band(visible);

        Assert.Equal(8, band.MaxPanel.X, Tolerance);
        Assert.Equal(344, band.MaxPanel.Width, Tolerance);
        Assert.Equal(480 - 8, band.MaxPanel.Y + band.MaxPanel.Height, Tolerance);
        Assert.Equal(144 - 8, band.MaxPanel.Height, Tolerance);
        Assert.Equal(16, band.CornerRadius, Tolerance);
        Assert.Equal(10, band.PaddingY, Tolerance);
        Assert.Equal(8 + 14, band.Text.X, Tolerance);
        Assert.Equal(344 - 28, band.Text.Width, Tolerance);
        Assert.Equal(144 - 8 - 20, band.Text.Height, Tolerance);
    }

    [Fact]
    public void Band_ScalesWithThePictureWidth()
    {
        var small = StoryTextLayout.Band(new RectD(0, 0, 100, 133));
        var large = StoryTextLayout.Band(new RectD(0, 0, 200, 266));

        Assert.Equal(small.CornerRadius * 2, large.CornerRadius, Tolerance);
        Assert.Equal(small.PaddingY * 2, large.PaddingY, Tolerance);
    }

    [Fact]
    public void Panel_HugsShortWords_AndNeverExceedsTheMaximum()
    {
        var band = StoryTextLayout.Band(new RectD(10, 20, 360, 480));

        var short1 = StoryTextLayout.Panel(band, 30);
        var tall = StoryTextLayout.Panel(band, 10_000);

        Assert.Equal(30 + 2 * band.PaddingY, short1.Height, Tolerance);
        Assert.Equal(band.MaxPanel.Y + band.MaxPanel.Height, short1.Y + short1.Height, Tolerance);
        Assert.Equal(band.MaxPanel, tall);
    }

    [Theory]
    [InlineData(PaperKind.A4, false)]
    [InlineData(PaperKind.A4, true)]
    [InlineData(PaperKind.Letter, false)]
    [InlineData(PaperKind.Letter, true)]
    public void TextPage_BoxStaysInsideItsHalfAndTheMargin(PaperKind paper, bool right)
    {
        var sheet = Sheet(paper);
        var half = HalfSheetLayout.HalfOf(sheet);

        var area = StoryTextLayout.TextPage(sheet, right, HalfSheetLayout.DefaultMarginMm, PageNumberPosition.Below);

        var left = right ? half.Width : 0;
        Assert.Equal(new RectD(left, 0, half.Width, half.Height), area.Page);
        Assert.True(area.Text.X >= left + HalfSheetLayout.DefaultMarginMm);
        Assert.True(area.Text.X + area.Text.Width <= left + half.Width - HalfSheetLayout.DefaultMarginMm);
        Assert.True(area.Text.Y >= HalfSheetLayout.DefaultMarginMm);
        Assert.True(area.Text.Y + area.Text.Height <= half.Height - HalfSheetLayout.DefaultMarginMm - PageNumbering.BandMm(sheet) + Tolerance);
    }

    [Fact]
    public void TextPage_LeavesTheNumberStripFree()
    {
        var sheet = Sheet(PaperKind.A4);
        var strip = HalfSheetLayout.PageNumberRect(sheet, false, HalfSheetLayout.DefaultMarginMm, PageNumberPosition.Below)!.Value;

        var area = StoryTextLayout.TextPage(sheet, false, HalfSheetLayout.DefaultMarginMm, PageNumberPosition.Below);

        Assert.True(area.Text.Y + area.Text.Height <= strip.Y + Tolerance);
    }

    [Fact]
    public void TextPage_InsetsTwelvePercentOfThePageWidth()
    {
        var sheet = Sheet(PaperKind.A4);

        var area = StoryTextLayout.TextPage(sheet, false, 0, PageNumberPosition.Off);

        Assert.Equal(0.12 * 148.5, area.Text.X, 1e-6);
    }

    [Theory]
    [InlineData(PaperKind.A4)]
    [InlineData(PaperKind.Letter)]
    public void Steps_AreLargestFirstWithTheMockSizes(PaperKind paper)
    {
        Assert.Equal([22, 20, 18, 16, 14], StoryTextLayout.BandStepsPt(paper));
        Assert.Equal([28, 24, 20, 18, 16], StoryTextLayout.PageStepsPt(paper));
    }

    [Fact]
    public void Choose_TakesTheFirstSizeThatFits()
    {
        var result = StoryTextLayout.Choose([22, 20, 18, 16, 14], pt => pt <= 18);

        Assert.Equal(new TextFitResult(18, true, true), result);
    }

    [Fact]
    public void Choose_KeepsTheNormalSizeWhenItFits()
    {
        Assert.Equal(new TextFitResult(22, true, false), StoryTextLayout.Choose([22, 20, 18], _ => true));
    }

    [Fact]
    public void Choose_FallsBackToTheSmallestSizeAndSaysItDoesNotFit()
    {
        Assert.Equal(new TextFitResult(14, false, true), StoryTextLayout.Choose([22, 20, 18, 16, 14], _ => false));
    }

    [Fact]
    public void Choose_RefusesNoSteps()
    {
        Assert.Throws<ArgumentException>(() => StoryTextLayout.Choose([], _ => true));
    }

    [Fact]
    public void PtToMm_IsTheTypographicPoint()
    {
        Assert.Equal(25.4, StoryTextLayout.PtToMm(72), Tolerance);
    }

    [Fact]
    public void CentredTop_CentresShortWords_AndTopAlignsLongOnes()
    {
        var box = new RectD(0, 10, 50, 100);

        Assert.Equal(40, StoryTextLayout.CentredTop(box, 40), Tolerance);
        Assert.Equal(10, StoryTextLayout.CentredTop(box, 150), Tolerance);
    }

    [Fact]
    public void BlackberryPicture_BandCoversNoMoreThanItsPlainBottom()
    {
        // docs/design/reference/blackberry-path.png is plain in its bottom ~30% (rows 70-85% and below); the band must stay inside the last 30% of the picture.
        var visible = Visible(PaperKind.A4, true, ScalingMode.Fit, PageNumberPosition.Off);

        var band = StoryTextLayout.Band(visible);

        var top = (band.MaxPanel.Y - visible.Y) / visible.Height;
        Assert.True(top >= 0.70 - Tolerance);
    }

    [Theory]
    [InlineData(TextBand.Top)]
    [InlineData(TextBand.Middle)]
    [InlineData(TextBand.Bottom)]
    public void EveryBand_StaysWithinThirtyPercentOfThePicture(TextBand which)
    {
        var visible = Visible(PaperKind.A4, true, ScalingMode.Fit, PageNumberPosition.Below);

        var band = StoryTextLayout.Band(visible, which);

        Assert.True(band.MaxPanel.Height <= 0.30 * visible.Height + Tolerance);
        Assert.True(band.MaxPanel.Y >= visible.Y);
        Assert.True(band.MaxPanel.Y + band.MaxPanel.Height <= visible.Y + visible.Height);
        Assert.Equal(which, band.Band);
    }

    [Fact]
    public void TopBand_SitsInTheTopThirty_AndMiddleBandIsCentred()
    {
        var visible = new RectD(10, 20, 360, 480);

        var top = StoryTextLayout.Band(visible, TextBand.Top);
        var middle = StoryTextLayout.Band(visible, TextBand.Middle);

        Assert.Equal(visible.Y + 8, top.MaxPanel.Y, 9);
        Assert.True(top.MaxPanel.Y + top.MaxPanel.Height <= visible.Y + 0.30 * visible.Height + Tolerance);
        Assert.Equal(visible.Y + visible.Height / 2, middle.MaxPanel.Y + middle.MaxPanel.Height / 2, 9);
        Assert.Equal(StoryTextLayout.Band(visible).MaxPanel.Height, top.MaxPanel.Height, 9);
    }

    [Fact]
    public void ShortText_PanelHugsTheTopAndBottomEdgesAndCentresInTheMiddle()
    {
        var visible = new RectD(0, 0, 360, 480);
        var top = StoryTextLayout.Band(visible, TextBand.Top);
        var middle = StoryTextLayout.Band(visible, TextBand.Middle);
        var bottom = StoryTextLayout.Band(visible);

        Assert.Equal(top.MaxPanel.Y, StoryTextLayout.Panel(top, 20).Y, 9);
        var centred = StoryTextLayout.Panel(middle, 20);
        Assert.Equal(visible.Height / 2, centred.Y + centred.Height / 2, 9);
        var low = StoryTextLayout.Panel(bottom, 20);
        Assert.Equal(bottom.MaxPanel.Y + bottom.MaxPanel.Height, low.Y + low.Height, 9);
    }
}
