using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Text;

/// <summary>How plain each band of a picture is, from 0 (busy) to 1 (empty).</summary>
public readonly record struct BandPlainness(double Top, double Middle, double Bottom)
{
    /// <summary>The plainness of one band.</summary>
    public double Of(TextBand band) => band switch
    {
        TextBand.Top => Top,
        TextBand.Middle => Middle,
        TextBand.Bottom => Bottom,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
    };

    /// <summary>
    /// The plainest band. The bottom stays the default: another band wins only when it is plainer by more than
    /// <see cref="BandSuggestion.BottomPreference"/>, and the top beats the middle on a tie.
    /// </summary>
    public TextBand Suggested
    {
        get
        {
            var best = Top >= Middle ? TextBand.Top : TextBand.Middle;
            return Of(best) > Bottom + BandSuggestion.BottomPreference ? best : TextBand.Bottom;
        }
    }
}

/// <summary>
/// Scores how plain each band of a picture is, on a (downscaled) copy given as a plain pixel buffer. A band is busy when many of
/// its pixels sit on an edge and when its colours vary a lot; the score is one minus that busyness.
/// </summary>
public static class BandSuggestion
{
    /// <summary>The bottom band is kept unless another is plainer by more than this.</summary>
    public const double BottomPreference = 0.05;

    private const int BytesPerPixel = 4;
    private const byte MinOpaqueAlpha = 8;
    private const double EdgeThreshold = 14;
    private const double EdgeShareForBusy = 0.2;
    private const double SpreadForBusy = 55;
    private const double EdgeWeight = 0.6;
    private const double SpreadWeight = 0.4;
    private const double RedLuma = 0.2126;
    private const double GreenLuma = 0.7152;
    private const double BlueLuma = 0.0722;
    private const int MinSide = 3;

    /// <summary>
    /// The plainness of the three bands of a picture given as straight BGRA pixels (<paramref name="stride"/> bytes per row), or
    /// null when the picture is too small to judge. Transparent pixels count as white paper.
    /// </summary>
    public static BandPlainness? Analyse(ReadOnlySpan<byte> pixels, int width, int height, int stride) =>
        Analyse(pixels, width, height, stride, PictureWindow.Whole);

    /// <summary>
    /// As above, but only the part of the picture inside <paramref name="window"/> is judged: the part that shows on the page
    /// (Fill scaling crops the rest), which is where the bands sit.
    /// </summary>
    public static BandPlainness? Analyse(ReadOnlySpan<byte> pixels, int width, int height, int stride, PictureWindow window)
    {
        if (width < MinSide || height < MinSide || pixels.Length < (height - 1) * stride + width * BytesPerPixel)
        {
            return null;
        }

        var (left, top, cropWidth, cropHeight) = window.ToPixels(width, height);
        if (cropWidth < MinSide || cropHeight < MinSide)
        {
            return null;
        }

        var luma = LumaOf(pixels, stride, left, top, cropWidth, cropHeight);
        return new BandPlainness(
            PlainnessOf(luma, cropWidth, StoryBands.Rows(cropHeight, TextBand.Top)),
            PlainnessOf(luma, cropWidth, StoryBands.Rows(cropHeight, TextBand.Middle)),
            PlainnessOf(luma, cropWidth, StoryBands.Rows(cropHeight, TextBand.Bottom)));
    }

    private static double[] LumaOf(ReadOnlySpan<byte> pixels, int stride, int left, int top, int width, int height)
    {
        var luma = new double[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = (top + y) * stride + (left + x) * BytesPerPixel;
                var opaque = pixels[at + 3] >= MinOpaqueAlpha;
                luma[y * width + x] = opaque ? RedLuma * pixels[at + 2] + GreenLuma * pixels[at + 1] + BlueLuma * pixels[at] : byte.MaxValue;
            }
        }

        return luma;
    }

    private static double PlainnessOf(double[] luma, int width, (int First, int End) rows)
    {
        double sum = 0, sumSquares = 0;
        var count = 0;
        var edges = 0;
        for (var y = rows.First; y < rows.End; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = luma[y * width + x];
                sum += value;
                sumSquares += value * value;
                count++;
                edges += IsEdge(luma, width, rows.End, x, y) ? 1 : 0;
            }
        }

        var mean = sum / count;
        var spread = Math.Sqrt(Math.Max(0, sumSquares / count - mean * mean));
        var busyness = EdgeWeight * Math.Min(1, edges / (double)count / EdgeShareForBusy) + SpreadWeight * Math.Min(1, spread / SpreadForBusy);
        return 1 - Math.Min(1, busyness);
    }

    private static bool IsEdge(double[] luma, int width, int endRow, int x, int y)
    {
        var here = luma[y * width + x];
        var right = x + 1 < width ? luma[y * width + x + 1] : here;
        var below = y + 1 < endRow ? luma[(y + 1) * width + x] : here;
        return Math.Abs(right - here) + Math.Abs(below - here) > EdgeThreshold;
    }
}

/// <summary>The rows of a picture that each band covers.</summary>
public static class StoryBands
{
    /// <summary>A band is at most this part of the picture's height.</summary>
    public const double MaxFraction = 0.30;

    private const double MiddleStart = 0.35;

    /// <summary>The first row and the row after the last of a band in a picture <paramref name="height"/> rows tall (never empty).</summary>
    public static (int First, int End) Rows(int height, TextBand band)
    {
        var size = Math.Max(1, (int)Math.Round(MaxFraction * height));
        var first = band switch
        {
            TextBand.Top => 0,
            TextBand.Middle => (int)Math.Round(MiddleStart * height),
            TextBand.Bottom => height - size,
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
        };
        first = Math.Clamp(first, 0, height - 1);
        return (first, Math.Min(height, first + size));
    }
}

/// <summary>The part of a picture that shows on the page, as fractions (0 to 1) of its width and height.</summary>
public readonly record struct PictureWindow(double X, double Y, double Width, double Height)
{
    /// <summary>The whole picture.</summary>
    public static PictureWindow Whole { get; } = new(0, 0, 1, 1);

    /// <summary>The window of the visible part of a placement, relative to the whole picture as printed.</summary>
    public static PictureWindow Of(RectD localClip, RectD image)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return Whole;
        }

        return new PictureWindow(localClip.X / image.Width, localClip.Y / image.Height, localClip.Width / image.Width, localClip.Height / image.Height);
    }

    /// <summary>The window as a pixel rectangle (left, top, width, height) inside a picture of the given size, kept inside it.</summary>
    public (int Left, int Top, int Width, int Height) ToPixels(int width, int height)
    {
        var left = Math.Clamp((int)Math.Round(X * width), 0, width - 1);
        var top = Math.Clamp((int)Math.Round(Y * height), 0, height - 1);
        var right = Math.Clamp((int)Math.Round((X + Width) * width), left + 1, width);
        var bottom = Math.Clamp((int)Math.Round((Y + Height) * height), top + 1, height);
        return (left, top, right - left, bottom - top);
    }
}
