using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>The settings file could not be read; it is left as it is, never overwritten.</summary>
public sealed class KdpSettingsException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>What the owner chose for one story page.</summary>
public sealed record KdpPageSettings([property: JsonPropertyName("cropOffsetPx"), JsonRequired] int CropOffsetPx);

/// <summary>
/// <c>kdp.json</c> beside a book's output (Slice 28): <c>{ "pages": { "7": { "cropOffsetPx": 60 } } }</c>, keyed by story page number (1-based, the
/// number the check report lists); since Slice 36 also <c>"covers": { "front": { "cropOffsetPx": 60 }, "back": { ... } }</c> for the two cover pictures
/// (a file without <c>covers</c> still reads). A picture with an entry is the owner's choice and is never flagged or changed again over it. Read and written only here.
/// </summary>
public sealed class KdpBookSettings
{
    public const string FileName = "kdp.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly SortedDictionary<int, KdpPageSettings> _pages;
    private readonly SortedDictionary<string, KdpPageSettings> _covers;

    public KdpBookSettings()
        : this([], [], null, null)
    {
    }

    private KdpBookSettings(IEnumerable<KeyValuePair<int, KdpPageSettings>> pages, IEnumerable<KeyValuePair<string, KdpPageSettings>> covers, string? bookId, string? bookTitle)
    {
        _pages = new SortedDictionary<int, KdpPageSettings>(pages.ToDictionary(p => p.Key, p => p.Value));
        _covers = new SortedDictionary<string, KdpPageSettings>(covers.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal);
        BookId = bookId;
        BookTitle = bookTitle;
    }

    private KdpBookSettings Copy() => new(_pages, _covers, BookId, BookTitle);

    public static KdpBookSettings Empty => new();

    /// <summary>The <c>DinoBook.Id</c> these choices were made for; null in a file that does not record one yet.</summary>
    public string? BookId { get; }

    /// <summary>The book's title when the file was written, for the person reading the message.</summary>
    public string? BookTitle { get; }

    /// <summary>A copy that records which book the choices belong to.</summary>
    public KdpBookSettings ForBook(string bookId, string bookTitle) => new(_pages, _covers, bookId, bookTitle);

    /// <summary>The choices belong to another book, so applying them would crop the wrong pictures; a file that records no book passes.</summary>
    /// <exception cref="KdpSettingsException">The file records a different book id.</exception>
    public void EnsureBook(string bookId, string bookTitle)
    {
        if (BookId is not null && !string.Equals(BookId, bookId, StringComparison.Ordinal))
        {
            throw new KdpSettingsException($"{FileName} was made for another book (\"{BookTitle ?? BookId}\"), not \"{bookTitle}\". Use another --out folder for this book; the file is left as it is.");
        }
    }

    /// <summary>True when the owner chose an offset for this story page (even 0).</summary>
    public bool HasOffset(int storyPage) => _pages.ContainsKey(storyPage);

    public int OffsetFor(int storyPage) => _pages.TryGetValue(storyPage, out var page) ? page.CropOffsetPx : 0;

    /// <summary>A copy with the page's offset set (never negative).</summary>
    public KdpBookSettings WithOffset(int storyPage, int offsetPx)
    {
        if (storyPage < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(storyPage), "Story pages are numbered from 1.");
        }

        var copy = Copy();
        copy._pages[storyPage] = new KdpPageSettings(Math.Max(0, offsetPx));
        return copy;
    }

    /// <summary>A copy with the page's entry removed, so the page is checked and flagged again (a page with no entry is unchanged).</summary>
    public KdpBookSettings Without(int storyPage)
    {
        var copy = Copy();
        copy._pages.Remove(storyPage);
        return copy;
    }

    /// <summary>The story pages that have an entry, in order.</summary>
    public IReadOnlyList<int> Pages => [.. _pages.Keys];

    /// <summary>True when the owner (or the crop agent) chose an offset for a cover picture (<see cref="KdpPictureKeys.Front"/> or <see cref="KdpPictureKeys.Back"/>).</summary>
    public bool HasCoverOffset(string coverKey) => _covers.ContainsKey(coverKey);

    public int CoverOffsetFor(string coverKey) => _covers.TryGetValue(coverKey, out var cover) ? cover.CropOffsetPx : 0;

    /// <summary>A copy with the cover picture's offset set (never negative).</summary>
    public KdpBookSettings WithCoverOffset(string coverKey, int offsetPx)
    {
        KdpPictureKeys.EnsureCover(coverKey);
        var copy = Copy();
        copy._covers[coverKey] = new KdpPageSettings(Math.Max(0, offsetPx));
        return copy;
    }

    /// <summary>A copy with the cover picture's entry removed (a cover with no entry is unchanged).</summary>
    public KdpBookSettings WithoutCover(string coverKey)
    {
        var copy = Copy();
        copy._covers.Remove(coverKey);
        return copy;
    }

    /// <summary>The cover keys that have an entry, in order.</summary>
    public IReadOnlyList<string> Covers => [.. _covers.Keys];

    public string ToJson()
    {
        var file = new SettingsFile
        {
            Book = BookId is null ? null : new BookIdentity { Id = BookId, Title = BookTitle },
            Pages = _pages.ToDictionary(p => p.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), p => p.Value),
            Covers = _covers.Count == 0 ? null : _covers.ToDictionary(c => c.Key, c => c.Value),
        };
        return JsonSerializer.Serialize(file, Options);
    }

    /// <exception cref="KdpSettingsException">The text is not a valid settings file.</exception>
    public static KdpBookSettings Parse(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<SettingsFile>(json, Options) ?? throw new KdpSettingsException($"{FileName} is empty.");
            var pages = new List<KeyValuePair<int, KdpPageSettings>>();
            foreach (var (key, page) in file.Pages ?? [])
            {
                if (!int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) || number < 1 || page is null || page.CropOffsetPx < 0)
                {
                    throw new KdpSettingsException($"{FileName} has a bad entry for page \"{key}\".");
                }

                pages.Add(new KeyValuePair<int, KdpPageSettings>(number, page));
            }

            var covers = new List<KeyValuePair<string, KdpPageSettings>>();
            foreach (var (key, cover) in file.Covers ?? [])
            {
                if (!KdpPictureKeys.IsCover(key) || cover is null || cover.CropOffsetPx < 0)
                {
                    throw new KdpSettingsException($"{FileName} has a bad entry for cover \"{key}\".");
                }

                covers.Add(new KeyValuePair<string, KdpPageSettings>(key, cover));
            }

            return new KdpBookSettings(pages, covers, file.Book?.Id, file.Book?.Title);
        }
        catch (JsonException ex)
        {
            throw new KdpSettingsException($"{FileName} is not valid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>The settings in the file, or none when it does not exist. A file that cannot be read throws, so it is never overwritten.</summary>
    public static KdpBookSettings Read(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new KdpSettingsException($"{FileName} could not be read: {ex.Message}", ex);
        }
    }

    /// <summary>Writes to a temporary name and moves it into place, so a cut-short write never leaves a truncated file that every later run rejects.</summary>
    public void Write(string path)
    {
        var temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, ToJson());
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new KdpSettingsException($"{FileName} could not be written: {ex.Message}", ex);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temporary file is not worth hiding the real error.
        }
    }

    private sealed class BookIdentity
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    private sealed class SettingsFile
    {
        [JsonPropertyName("book")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BookIdentity? Book { get; set; }

        [JsonPropertyName("pages")]
        public Dictionary<string, KdpPageSettings>? Pages { get; set; }

        [JsonPropertyName("covers")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, KdpPageSettings>? Covers { get; set; }
    }
}
