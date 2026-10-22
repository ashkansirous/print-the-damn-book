using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PrintTheDamnBook.App.Services;

/// <summary>Decodes small frozen thumbnails of page images, without locking the file.</summary>
public sealed class ThumbnailLoader
{
    public const int DecodeWidth = 360;

    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Decodes every path once (off the UI thread) so later <see cref="Load"/> calls hit the cache.</summary>
    public void Preload(IEnumerable<string?> paths)
    {
        foreach (var path in paths.OfType<string>())
        {
            Load(path);
        }
    }

    public ImageSource? Load(string? path)
    {
        if (path is null)
        {
            return null;
        }

        if (_cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var image = Decode(path);
        _cache[path] = image;
        return image;
    }

    private static BitmapImage? Decode(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.DecodePixelWidth = DecodeWidth;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is COMException or IOException or NotSupportedException or UnauthorizedAccessException or FileFormatException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}
