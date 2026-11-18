using System.Collections.Concurrent;
using System.Windows.Media;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Services;

/// <summary>Decodes small frozen thumbnails of page images, without locking the file.</summary>
public sealed class ThumbnailLoader
{
    public const int DecodeWidth = 360;

    /// <summary>One entry per picture, holding the thumbnail and the real size together, written in one step (never half filled).</summary>
    private readonly ConcurrentDictionary<string, LoadedPicture> _pictures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Decodes every path once (off the UI thread) so later <see cref="Load"/> calls hit the cache.</summary>
    public void Preload(IEnumerable<string?> paths)
    {
        foreach (var path in paths.OfType<string>())
        {
            Load(path);
        }
    }

    /// <summary>Lets go of every decoded thumbnail, for a book that is going away.</summary>
    public void Clear()
    {
        _pictures.Clear();
    }

    public ImageSource? Load(string? path)
    {
        if (path is null)
        {
            return null;
        }

        if (_pictures.TryGetValue(path, out var cached))
        {
            return cached.Thumbnail;
        }

        var decoded = ImageDecoder.DecodeWithSize(path, DecodeWidth);
        return _pictures.GetOrAdd(path, new LoadedPicture(decoded?.Image, decoded?.PixelSize)).Thumbnail;
    }

    /// <summary>What was loaded for the picture, or null when it has not been loaded yet. Never decodes, so it is safe on the UI thread.</summary>
    public LoadedPicture? Peek(string? path)
    {
        return path is not null && _pictures.TryGetValue(path, out var loaded) ? loaded : null;
    }

    /// <summary>
    /// The picture's real pixel size, read from its header when the thumbnail was loaded (off the UI thread), so a fit decided on a screen
    /// matches the print, which lays the picture out at its real size. Null for a picture not loaded yet or unreadable: use the thumbnail's.
    /// </summary>
    public SizeD? RealSizeOf(string? path)
    {
        return Peek(path)?.RealSize;
    }
}

/// <summary>A picture's thumbnail and real pixel size; both null when it could not be read.</summary>
public sealed record LoadedPicture(ImageSource? Thumbnail, SizeD? RealSize);
