using System.Windows.Media;
using System.Windows.Media.Imaging;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.App.Services;

/// <summary>A decoded picture as straight BGRA pixels, small enough to keep (it is the thumbnail).</summary>
internal sealed record PictureSample(byte[] Pixels, int Width, int Height, int Stride);

/// <summary>
/// Hands the pixels of an already decoded picture (the thumbnail, which <see cref="ImageDecoder"/> made) to Core's
/// <see cref="BandSuggestion"/>. <see cref="Sample"/> is safe to call off the UI thread: the bitmap is frozen and nothing here touches the UI.
/// </summary>
internal static class BandAnalyzer
{
    private const int BytesPerPixel = 4;

    /// <summary>The picture's pixels, or null when it could not be read.</summary>
    public static PictureSample? Sample(ImageSource? picture)
    {
        if (picture is not BitmapSource bitmap)
        {
            return null;
        }

        if (!bitmap.IsFrozen)
        {
            // A bitmap still owned by another thread cannot be read here; freezing makes it safe to share (it throws when it cannot be).
            bitmap.Freeze();
        }

        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * BytesPerPixel;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return new PictureSample(pixels, converted.PixelWidth, converted.PixelHeight, stride);
    }

    /// <summary>How plain each band of the part of the picture inside <paramref name="window"/> is, or null when it is too small to judge.</summary>
    public static BandPlainness? Analyse(PictureSample sample, PictureWindow window)
    {
        return BandSuggestion.Analyse(sample.Pixels, sample.Width, sample.Height, sample.Stride, window);
    }
}
