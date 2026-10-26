using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests;

public class BandSuggestionTests
{
    private const int Width = 60;
    private const int Height = 80;
    private const int Bytes = 4;

    /// <summary>A flat grey picture; the rows <paramref name="busyRows"/> accepts are filled with a hard checkerboard instead.</summary>
    private static byte[] Picture(Func<int, bool> busyRows, byte flat = 200)
    {
        var pixels = new byte[Width * Height * Bytes];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var value = busyRows(y) ? ((x + y) % 2 == 0 ? (byte)10 : (byte)245) : flat;
                var at = (y * Width + x) * Bytes;
                pixels[at] = pixels[at + 1] = pixels[at + 2] = value;
                pixels[at + 3] = 255;
            }
        }

        return pixels;
    }

    private static BandPlainness Analyse(byte[] pixels) => BandSuggestion.Analyse(pixels, Width, Height, Width * Bytes)!.Value;

    [Fact]
    public void FlatPicture_IsPlainEverywhereAndSuggestsTheBottom()
    {
        var result = Analyse(Picture(_ => false));

        Assert.True(result.Top > 0.99 && result.Middle > 0.99 && result.Bottom > 0.99);
        Assert.Equal(TextBand.Bottom, result.Suggested);
    }

    [Fact]
    public void BusyBand_ScoresFarBelowAPlainOne()
    {
        var result = Analyse(Picture(y => y >= 0.70 * Height));

        Assert.True(result.Bottom < 0.1);
        Assert.True(result.Top > 0.9);
    }

    [Fact]
    public void EmptySkyAtTheTop_SuggestsTheTop()
    {
        var result = Analyse(Picture(y => y >= 0.30 * Height));

        Assert.Equal(TextBand.Top, result.Suggested);
    }

    [Fact]
    public void PlainMiddleBetweenBusyEnds_SuggestsTheMiddle()
    {
        var result = Analyse(Picture(y => y < 0.30 * Height || y >= 0.70 * Height));

        Assert.Equal(TextBand.Middle, result.Suggested);
    }

    [Fact]
    public void PlainBottomWithBusyTop_SuggestsTheBottom()
    {
        var result = Analyse(Picture(y => y < 0.70 * Height));

        Assert.Equal(TextBand.Bottom, result.Suggested);
    }

    [Fact]
    public void AnotherBandMustBeClearlyPlainerThanTheBottom()
    {
        var scores = new BandPlainness(Top: 0.80, Middle: 0.50, Bottom: 0.78);

        Assert.Equal(TextBand.Bottom, scores.Suggested);
        Assert.Equal(TextBand.Top, (scores with { Top = 0.90 }).Suggested);
    }

    [Fact]
    public void TheTopBeatsTheMiddleOnATie()
    {
        Assert.Equal(TextBand.Top, new BandPlainness(0.9, 0.9, 0.2).Suggested);
    }

    [Fact]
    public void SoftGradient_CountsAsPlain()
    {
        var pixels = Picture(_ => false);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var at = (y * Width + x) * Bytes;
                pixels[at] = pixels[at + 1] = pixels[at + 2] = (byte)(150 + y / 2);
            }
        }

        Assert.True(Analyse(pixels).Bottom > 0.5);
    }

    [Fact]
    public void TransparentPixels_CountAsPlainPaper()
    {
        var pixels = new byte[Width * Height * Bytes];

        Assert.True(Analyse(pixels).Top > 0.99);
    }

    [Fact]
    public void TooSmallOrShortBuffer_GivesNull()
    {
        Assert.Null(BandSuggestion.Analyse(new byte[16], 2, 2, 8));
        Assert.Null(BandSuggestion.Analyse(new byte[10], Width, Height, Width * Bytes));
    }

    [Theory]
    [InlineData(TextBand.Top)]
    [InlineData(TextBand.Middle)]
    [InlineData(TextBand.Bottom)]
    public void BandRows_AreAtMostThirtyPercentAndInsideThePicture(TextBand band)
    {
        var (first, end) = StoryBands.Rows(Height, band);

        Assert.InRange(first, 0, Height - 1);
        Assert.True(end <= Height);
        Assert.True(end - first <= 0.30 * Height + 1);
        Assert.True(end > first);
    }

    [Fact]
    public void StoryText_UsesThePickBeforeTheSuggestionBeforeTheBottom()
    {
        var measured = new BandPlainness(0.9, 0.3, 0.2);
        var picked = new StoryText("x", TextPlacement.OnPicture, TextBand.Middle) { Plainness = measured };

        Assert.Equal(TextBand.Bottom, new StoryText("x").EffectiveBand);
        Assert.Equal(TextBand.Top, (new StoryText("x") { Plainness = measured }).EffectiveBand);
        Assert.Equal(TextBand.Middle, picked.EffectiveBand);
        Assert.Equal(0.3, picked.EffectivePlainness);
    }

    [Fact]
    public void PanelOpacity_FollowsPlainnessAndIsClamped()
    {
        Assert.Equal(0.97, StoryTextLayout.OpacityFor(0), 9);
        Assert.Equal(0.85, StoryTextLayout.OpacityFor(1), 9);
        Assert.Equal(0.91, StoryTextLayout.OpacityFor(0.5), 9);
        Assert.Equal(0.97, StoryTextLayout.OpacityFor(-3), 9);
        Assert.Equal(0.85, StoryTextLayout.OpacityFor(7), 9);
        Assert.Equal(StoryTextLayout.PanelOpacity, StoryTextLayout.OpacityFor(null));
    }

    [Fact]
    public void PanelOpacity_OfSyntheticBands_IsHigherOverTheBusyOne()
    {
        var result = Analyse(Picture(y => y >= 0.70 * Height));

        Assert.True(StoryTextLayout.OpacityFor(result.Bottom) > StoryTextLayout.OpacityFor(result.Top));
        Assert.InRange(StoryTextLayout.OpacityFor(result.Bottom), 0.85, 0.97);
    }

    [Fact]
    public void FixedDinoOpacity_SitsInsideTheRange()
    {
        Assert.InRange(StoryTextLayout.PanelOpacity, StoryTextLayout.PlainPanelOpacity, StoryTextLayout.BusyPanelOpacity);
    }

    [Fact]
    public void Window_JudgesOnlyTheVisiblePart_NotTheCroppedRows()
    {
        // Busy rows at the very bottom are cropped away by Fill scaling: the visible part is plain everywhere.
        var pixels = Picture(y => y >= 0.85 * Height);
        var whole = Analyse(pixels);
        var visible = BandSuggestion.Analyse(pixels, Width, Height, Width * Bytes, new PictureWindow(0, 0, 1, 0.8))!.Value;

        Assert.True(whole.Bottom < 0.9);
        Assert.True(visible.Bottom > 0.99);
        Assert.Equal(TextBand.Bottom, visible.Suggested);
    }

    [Fact]
    public void Window_CroppedTopRowsNoLongerCountAsTheTopBand()
    {
        var pixels = Picture(y => y < 0.15 * Height);
        var visible = BandSuggestion.Analyse(pixels, Width, Height, Width * Bytes, new PictureWindow(0, 0.2, 1, 0.8))!.Value;

        Assert.True(visible.Top > 0.99);
    }

    [Fact]
    public void WindowOf_IsTheLocalClipAsFractionsOfTheImage()
    {
        var window = PictureWindow.Of(new RectD(0, 10, 100, 80), new RectD(0, 0, 100, 100));

        Assert.Equal(new PictureWindow(0, 0.1, 1, 0.8), window);
    }

    [Fact]
    public void TinyWindow_IsTooSmallToJudge()
    {
        var pixels = Picture(_ => false);

        Assert.Null(BandSuggestion.Analyse(pixels, Width, Height, Width * Bytes, new PictureWindow(0, 0, 0.01, 0.01)));
    }
}
