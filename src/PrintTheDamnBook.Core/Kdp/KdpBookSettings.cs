using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>The settings file could not be read; it is left as it is, never overwritten.</summary>
public sealed class KdpSettingsException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>What the owner chose for one story page.</summary>
public sealed record KdpPageSettings([property: JsonPropertyName("cropOffsetPx"), JsonRequired] int CropOffsetPx);

/// <summary>
/// What was chosen for one cover picture (Slice 36 and 37): its crop offset, the words' box and colours (<c>text</c>) and, on the back, the colours of the
/// Dino line beside the QR code (<c>dino</c>). Every part is optional, so a file from Slice 36 (an offset only) still reads.
/// </summary>
public sealed record KdpCoverSettings(
    [property: JsonPropertyName("cropOffsetPx"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CropOffsetPx = null,
    [property: JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] KdpCoverText? Text = null,
    [property: JsonPropertyName("dino"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] KdpDinoText? Dino = null)
{
    internal bool IsEmpty => CropOffsetPx is null && Text is null && Dino is null;
}

/// <summary>
/// <c>kdp.json</c> beside a book's output (Slice 28): <c>{ "pages": { "7": { "cropOffsetPx": 60 } } }</c>, keyed by story page number (1-based, the
/// number the check report lists); since Slice 36 also <c>"covers": { "front": { "cropOffsetPx": 60 }, "back": { ... } }</c> for the two cover pictures
/// (a file without <c>covers</c> still reads) and since Slice 37 per cover <c>"text": { "box": {left, top, width, height}, "color": "#FFFFFF", "shadow": "#000000" }</c>
/// and, on the back, <c>"dino": { "color", "shadow" }</c> (since Slice 41 also <c>"box"</c> and <c>"forOffsetPx"</c>, the Dino block's place on the back). A picture with an entry is the owner's choice and is never flagged or changed again over it. Read and written only here.
/// </summary>
public sealed class KdpBookSettings
{
    public const string FileName = "kdp.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly SortedDictionary<int, KdpPageSettings> _pages;
    private readonly SortedDictionary<string, KdpCoverSettings> _covers;

    public KdpBookSettings()
        : this([], [], null, null)
    {
    }

    private KdpBookSettings(IEnumerable<KeyValuePair<int, KdpPageSettings>> pages, IEnumerable<KeyValuePair<string, KdpCoverSettings>> covers, string? bookId, string? bookTitle)
    {
        _pages = new SortedDictionary<int, KdpPageSettings>(pages.ToDictionary(p => p.Key, p => p.Value));
        _covers = new SortedDictionary<string, KdpCoverSettings>(covers.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal);
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
    public bool HasCoverOffset(string coverKey) => _covers.TryGetValue(coverKey, out var cover) && cover.CropOffsetPx is not null;

    public int CoverOffsetFor(string coverKey) => _covers.TryGetValue(coverKey, out var cover) ? cover.CropOffsetPx ?? 0 : 0;

    /// <summary>A copy with the cover picture's offset set (never negative); its text choices stay.</summary>
    public KdpBookSettings WithCoverOffset(string coverKey, int offsetPx)
    {
        return WithCover(coverKey, cover => cover with { CropOffsetPx = Math.Max(0, offsetPx) });
    }

    /// <summary>A copy with the cover picture's offset removed (its text choices stay; a cover left with nothing loses its entry).</summary>
    public KdpBookSettings WithoutCover(string coverKey)
    {
        return WithCover(coverKey, cover => cover with { CropOffsetPx = null });
    }

    /// <summary>The words' box and colours chosen for a cover; null when none were chosen (the Slice 36 rule places them).</summary>
    public KdpCoverText? CoverText(string coverKey) => _covers.TryGetValue(coverKey, out var cover) ? cover.Text : null;

    /// <summary>True when the cover's saved text choice was decided for a different crop offset than the one in effect now, so its box no longer matches the picture.</summary>
    public bool CoverTextStale(string coverKey) => CoverText(coverKey) is { ForOffsetPx: { } forOffset } && forOffset != CoverOffsetFor(coverKey);

    /// <summary>The cover's saved text choice when it still fits the crop window; null when none is saved or it is <see cref="CoverTextStale"/>.</summary>
    public KdpCoverText? UsableCoverText(string coverKey) => CoverTextStale(coverKey) ? null : CoverText(coverKey);

    /// <summary>A copy with the cover's text choice set.</summary>
    /// <exception cref="KdpSettingsException">The box or a colour is not valid.</exception>
    public KdpBookSettings WithCoverText(string coverKey, KdpCoverText text)
    {
        ValidateText(coverKey, text);
        return WithCover(coverKey, cover => cover with { Text = text with { Color = Hex(text.Color), Shadow = Hex(text.Shadow) } });
    }

    /// <summary>A copy with the cover's text choice removed (the fallback places the words again).</summary>
    public KdpBookSettings WithoutCoverText(string coverKey)
    {
        return WithCover(coverKey, cover => cover with { Text = null });
    }

    /// <summary>The colours of the Dino line beside the back cover's QR code; null when none were chosen.</summary>
    public KdpTextColors? DinoColors => DinoText?.Colors;

    /// <summary>The Dino block's colours and, when chosen, its box (Slice 41); null when nothing was chosen.</summary>
    public KdpDinoText? DinoText => _covers.TryGetValue(KdpPictureKeys.Back, out var cover) ? cover.Dino : null;

    /// <summary>True when the Dino block's saved box was decided for a different back crop offset than the one in effect now.</summary>
    public bool DinoBoxStale => DinoText is { Box: not null, ForOffsetPx: { } forOffset } && forOffset != CoverOffsetFor(KdpPictureKeys.Back);

    /// <summary>The Dino text with its box dropped when that box is <see cref="DinoBoxStale"/> (the colours always stay); null when none was chosen.</summary>
    public KdpDinoText? UsableDinoText => DinoBoxStale ? DinoText! with { Box = null, ForOffsetPx = null } : DinoText;

    /// <summary>A copy with the Dino line's colours set (back cover only); a saved box stays.</summary>
    /// <exception cref="KdpSettingsException">A colour is not valid.</exception>
    public KdpBookSettings WithDinoColors(KdpTextColors colors)
    {
        return WithCover(KdpPictureKeys.Back, cover => cover with { Dino = (cover.Dino ?? new KdpDinoText(colors.Color, colors.Shadow)) with { Color = Hex(colors.Color), Shadow = Hex(colors.Shadow) } });
    }

    /// <summary>A copy with the Dino block's colours and box set (back cover only); the box is tied to <paramref name="forOffsetPx"/>.</summary>
    /// <exception cref="KdpSettingsException">A colour or the box is not valid.</exception>
    public KdpBookSettings WithDinoText(KdpTextColors colors, KdpTextBox box, int forOffsetPx)
    {
        if (!box.IsValid)
        {
            throw new KdpSettingsException("The Dino box of the back cover must lie inside the panel (fractions from 0 to 1, with a width and a height).");
        }

        return WithCover(KdpPictureKeys.Back, cover => cover with { Dino = new KdpDinoText(Hex(colors.Color), Hex(colors.Shadow), box, forOffsetPx) });
    }

    /// <summary>A copy with the Dino block's choice (colours and box) removed.</summary>
    public KdpBookSettings WithoutDinoColors()
    {
        return WithCover(KdpPictureKeys.Back, cover => cover with { Dino = null });
    }

    private KdpBookSettings WithCover(string coverKey, Func<KdpCoverSettings, KdpCoverSettings> change)
    {
        KdpPictureKeys.EnsureCover(coverKey);
        var copy = Copy();
        var changed = change(_covers.TryGetValue(coverKey, out var existing) ? existing : new KdpCoverSettings());
        if (changed.IsEmpty)
        {
            copy._covers.Remove(coverKey);
        }
        else
        {
            copy._covers[coverKey] = changed;
        }

        return copy;
    }

    private static string Hex(string color)
    {
        return KdpHexColor.TryNormalize(color, out var normalized) ? normalized : throw new KdpSettingsException($"\"{color}\" is not a colour; use #RRGGBB.");
    }

    private static void ValidateText(string coverKey, KdpCoverText text)
    {
        if (!text.Box.IsValid)
        {
            throw new KdpSettingsException($"The text box of the {coverKey} cover must lie inside the panel (fractions from 0 to 1, with a width and a height).");
        }

        _ = Hex(text.Color);
        _ = Hex(text.Shadow);
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

            var covers = new List<KeyValuePair<string, KdpCoverSettings>>();
            foreach (var (key, cover) in file.Covers ?? [])
            {
                if (!KdpPictureKeys.IsCover(key) || cover is null || cover.CropOffsetPx < 0 || !IsValidCover(key, cover))
                {
                    throw new KdpSettingsException($"{FileName} has a bad entry for cover \"{key}\".");
                }

                var kept = Normalized(cover);
                if (!kept.IsEmpty)
                {
                    covers.Add(new KeyValuePair<string, KdpCoverSettings>(key, kept));
                }
            }

            return new KdpBookSettings(pages, covers, file.Book?.Id, file.Book?.Title);
        }
        catch (JsonException ex)
        {
            throw new KdpSettingsException($"{FileName} is not valid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>The text choice has a valid box and colours, and only the back has a Dino line.</summary>
    private static bool IsValidCover(string key, KdpCoverSettings cover)
    {
        var textOk = cover.Text is null || (cover.Text.Box is { IsValid: true } && KdpHexColor.TryNormalize(cover.Text.Color, out _) && KdpHexColor.TryNormalize(cover.Text.Shadow, out _));
        var dinoOk = cover.Dino is null || (key == KdpPictureKeys.Back && KdpHexColor.TryNormalize(cover.Dino.Color, out _) && KdpHexColor.TryNormalize(cover.Dino.Shadow, out _) && (cover.Dino.Box?.IsValid ?? true));
        return textOk && dinoOk;
    }

    private static KdpCoverSettings Normalized(KdpCoverSettings cover)
    {
        return cover with
        {
            Text = cover.Text is null ? null : cover.Text with { Color = Hex(cover.Text.Color), Shadow = Hex(cover.Text.Shadow) },
            Dino = cover.Dino is null ? null : cover.Dino with { Color = Hex(cover.Dino.Color), Shadow = Hex(cover.Dino.Shadow) },
        };
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
        public Dictionary<string, KdpCoverSettings>? Covers { get; set; }
    }
}
