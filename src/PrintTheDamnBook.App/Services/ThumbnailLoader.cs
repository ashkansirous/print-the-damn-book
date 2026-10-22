using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PrintTheDamnBook.App.Services;

/// <summary>Decodes small frozen thumbnails of page images, without locking the file.</summary>
public sealed class ThumbnailLoader
{
    public const int DecodeWidth = 360;

    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Decodes every path once (off the UI thread) and returns the paths that could not be decoded.</summary>
    public IReadOnlyList<string> Preload(IEnumerable<string?> paths) =>
        paths.OfType<string>().Where(p => Load(p) is null).ToList();

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

    private static BitmapSource? Decode(string path)
    {
        return ImageDecoder.Decode(path, DecodeWidth);
    }
}
