using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Services;

/// <summary>Reads page images for the preview and for printing, without locking the file. Unreadable files give null.</summary>
public static class ImageDecoder
{
    private const string OrientationQuery = "System.Photo.Orientation";
    private const int NormalOrientation = 1;

    /// <summary>Decodes a frozen bitmap; a positive <paramref name="decodePixelWidth"/> decodes it scaled to that width.</summary>
    public static BitmapSource? Decode(string path, int decodePixelWidth = 0)
    {
        try
        {
            var orientation = ReadHeader(path).Orientation;
            using var stream = OpenRead(path);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            ApplyDecodeWidth(bitmap, decodePixelWidth, SwapsAxes(orientation));
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return Orient(bitmap, orientation);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return null;
        }
    }

    /// <summary>The pixel size of the image, read from its header without decoding the pixels.</summary>
    public static SizeD? ReadPixelSize(string path)
    {
        try
        {
            var (width, height, orientation) = ReadHeader(path);
            return SwapsAxes(orientation) ? new SizeD(height, width) : new SizeD(width, height);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return null;
        }
    }

    private static (int Width, int Height, int Orientation) ReadHeader(string path)
    {
        using var stream = OpenRead(path);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        return (frame.PixelWidth, frame.PixelHeight, ReadOrientation(frame));
    }

    private static int ReadOrientation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata && metadata.ContainsQuery(OrientationQuery)
                && metadata.GetQuery(OrientationQuery) is ushort value && value is >= 1 and <= 8)
            {
                return value;
            }
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return NormalOrientation;
        }

        return NormalOrientation;
    }

    private static bool SwapsAxes(int orientation)
    {
        return orientation >= 5;
    }

    private static void ApplyDecodeWidth(BitmapImage bitmap, int displayedWidth, bool swapped)
    {
        if (displayedWidth <= 0)
        {
            return;
        }

        if (swapped)
        {
            bitmap.DecodePixelHeight = displayedWidth;
        }
        else
        {
            bitmap.DecodePixelWidth = displayedWidth;
        }
    }

    /// <summary>Applies the EXIF orientation (1-8) so the image is upright.</summary>
    private static BitmapSource Orient(BitmapImage bitmap, int orientation)
    {
        if (orientation == NormalOrientation)
        {
            return bitmap;
        }

        var transform = new TransformGroup();
        if (orientation is 2 or 5 or 7)
        {
            transform.Children.Add(new ScaleTransform(-1, 1));
        }

        if (orientation == 4)
        {
            transform.Children.Add(new ScaleTransform(1, -1));
        }

        var angle = orientation switch { 3 => 180, 6 or 7 => 90, 5 or 8 => 270, _ => 0 };
        if (angle != 0)
        {
            transform.Children.Add(new RotateTransform(angle));
        }

        var oriented = new TransformedBitmap(bitmap, transform);
        oriented.Freeze();
        return oriented;
    }

    private static FileStream OpenRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private static bool IsReadFailure(Exception ex)
    {
        return ex is COMException or IOException or NotSupportedException or UnauthorizedAccessException
            or FileFormatException or InvalidOperationException or ArgumentException;
    }
}
