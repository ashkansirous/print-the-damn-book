using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PrintTheDamnBook.Core.Dino;

/// <summary>One free story in Dino's store (Slice 18): the list line, checked. Both URLs are absolute and already on the store's host.</summary>
public sealed record DinoStoreEntry(
    string Id, string Title, string Language, int Version, int? PageCount, int? AgeMin, int? AgeMax, Uri? CoverUrl, Uri DownloadUrl);

/// <summary>A language the store offers, with how many books are in it.</summary>
public sealed record DinoStoreLanguage(string Code, string Name, int Count);

/// <summary>
/// The store's list (<c>GET /api/stories</c>), read defensively (Slice 18). The body is untrusted: it is capped, an entry with a bad field is
/// dropped without spoiling the list, an entry from a newer <c>.dino</c> schema is hidden, and every URL must be an <c>https</c> one on the
/// store's own host under <c>/api/stories/</c> (a relative one is resolved against it).
/// </summary>
public sealed class DinoStoreCatalog
{
    public const string Host = "dinoreader.sirous.uk";
    public const string BaseAddress = "https://" + Host;
    public const string ListPath = "/api/stories";
    public const string StoriesPrefix = "/api/stories/";
    public const int MaxCatalogBytes = 1024 * 1024;
    public const int MaxEntries = 500;

    private const string StoriesProperty = "stories";
    private const string DownloadSegment = "download";
    private const int MaxTitleChars = 200;
    private const int MaxLanguageChars = 16;
    private const int MaxAge = 18;
    private const int MaxVersion = 1_000_000;
    private const int MaxPageCount = DinoBookReader.MaxPages;
    private const string NotAList = "Dino's store sent something that is not a list of stories.";
    private const string TooLarge = "Dino's store sent a list that is too large.";

    private static readonly Regex SafeId = new(@"\A[A-Za-z0-9_-]{1,64}\z", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private static readonly Uri Base = new(BaseAddress);

    private DinoStoreCatalog(IReadOnlyList<DinoStoreEntry> entries)
    {
        Entries = entries;
    }

    /// <summary>The books this app can read, in the store's order.</summary>
    public IReadOnlyList<DinoStoreEntry> Entries { get; }

    /// <summary>Reads a list response. Throws <see cref="DinoBookException"/> when the body is too large or is no list; a malformed entry is skipped.</summary>
    public static DinoStoreCatalog Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxCatalogBytes)
        {
            throw new DinoBookException(TooLarge);
        }

        try
        {
            using var document = JsonDocument.Parse(json.TrimStart('﻿'));
            return new DinoStoreCatalog(ReadEntries(document.RootElement));
        }
        catch (JsonException ex)
        {
            throw new DinoBookException(NotAList, ex);
        }
    }

    /// <summary>True for an <c>https</c> URL on the store's host whose path is under <c>/api/stories/</c>; the one check for covers, downloads and redirects.</summary>
    public static bool IsAllowedUrl(Uri? url)
    {
        return url is { IsAbsoluteUri: true } && url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort && string.IsNullOrEmpty(url.UserInfo)
            && string.Equals(url.Host, Host, StringComparison.OrdinalIgnoreCase)
            && url.AbsolutePath.StartsWith(StoriesPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a book can be chosen: false for a language this app cannot print yet. Right-to-left languages are not printable until Slice 17
    /// lands; then only the ones it cannot lay out stay off (Hebrew), and this is the one place to change.
    /// </summary>
    public static bool IsSupported(DinoStoreEntry entry)
    {
        return !DinoBook.DescribeLanguage(entry.Language).IsRightToLeft;
    }

    /// <summary>Each language in the list with its book count, in the order first met.</summary>
    public IReadOnlyList<DinoStoreLanguage> Languages()
    {
        return Entries.GroupBy(e => DinoBook.Primary(e.Language))
            .Select(g => new DinoStoreLanguage(g.Key, DinoBook.DescribeLanguage(g.Key).Name, g.Count()))
            .ToList();
    }

    /// <summary>The books in <paramref name="languageCode"/>, or all of them when it is null or empty.</summary>
    public IReadOnlyList<DinoStoreEntry> InLanguage(string? languageCode)
    {
        var primary = DinoBook.Primary(languageCode);
        return primary.Length == 0 ? Entries : Entries.Where(e => DinoBook.Primary(e.Language) == primary).ToList();
    }

    /// <summary>"ages 4–7" or "ages 4"; empty when the store gives no (or an inconsistent) age range.</summary>
    public static string AgeText(DinoStoreEntry entry)
    {
        return (entry.AgeMin, entry.AgeMax) switch
        {
            (int min, int max) when min == max => $"ages {min}",
            (int min, int max) => $"ages {min}–{max}",
            _ => string.Empty,
        };
    }

    /// <summary>"12 pages · ages 4–7": the line under a book's language on its card.</summary>
    public static string MetaText(DinoStoreEntry entry)
    {
        var parts = new List<string>();
        if (entry.PageCount is { } pages)
        {
            parts.Add(pages == 1 ? "1 page" : $"{pages} pages");
        }

        var age = AgeText(entry);
        if (age.Length > 0)
        {
            parts.Add(age);
        }

        return string.Join(" · ", parts);
    }

    private static List<DinoStoreEntry> ReadEntries(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(NotAList);
        }

        if (!root.TryGetProperty(StoriesProperty, out var stories) || stories.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return stories.EnumerateArray().Take(MaxEntries).Select(EntryOf).OfType<Parsed>().Where(p => p.SchemaVersion <= DinoBookReader.SupportedSchemaVersion).Select(p => p.Entry).DistinctBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Parsed? EntryOf(JsonElement element)
    {
        try
        {
            var dto = element.Deserialize<EntryDto>(Options);
            return dto is null ? null : Validate(dto);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Parsed? Validate(EntryDto dto)
    {
        if (!IsValidText(dto.Id, SafeId) || !IsPlain(dto.Title, MaxTitleChars) || !IsPlain(dto.Language, MaxLanguageChars) || dto.SchemaVersion < 1 || dto.Version is < 1 or > MaxVersion)
        {
            return null;
        }

        var download = string.IsNullOrWhiteSpace(dto.DownloadUrl) ? DefaultUrl(dto.Id!, DownloadSegment) : ResolveUrl(dto.DownloadUrl);
        if (download is null || !IsAllowedUrl(download))
        {
            return null;
        }

        var cover = dto.CoverUrl is null ? null : ResolveUrl(dto.CoverUrl);
        var (min, max) = AgesOf(dto);
        var entry = new DinoStoreEntry(dto.Id!, dto.Title!.Trim(), dto.Language!.Trim(), dto.Version, PagesOf(dto.PageCount), min, max, IsAllowedUrl(cover) ? cover : null, download);
        return new Parsed(entry, dto.SchemaVersion);
    }

    private static bool IsValidText(string? text, Regex pattern)
    {
        return text is not null && pattern.IsMatch(text);
    }

    private static bool IsPlain(string? text, int maxChars)
    {
        return !string.IsNullOrWhiteSpace(text) && text.Length <= maxChars && !text.Any(char.IsControl);
    }

    private static int? PagesOf(int? pages)
    {
        return pages is >= 0 and <= MaxPageCount ? pages : null;
    }

    private static (int? Min, int? Max) AgesOf(EntryDto dto)
    {
        return dto.AgeMin is >= 0 and <= MaxAge && dto.AgeMax is >= 0 and <= MaxAge && dto.AgeMin <= dto.AgeMax ? (dto.AgeMin, dto.AgeMax) : (null, null);
    }

    private static Uri? ResolveUrl(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) && Uri.TryCreate(Base, text, out var url) ? url : null;
    }

    private static Uri DefaultUrl(string id, string segment)
    {
        return new Uri(Base, $"{StoriesPrefix}{id}/{segment}");
    }

    private sealed record Parsed(DinoStoreEntry Entry, int SchemaVersion);

    private sealed class EntryDto
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Language { get; set; }

        public int SchemaVersion { get; set; } = 1;

        public int Version { get; set; } = 1;

        public int? PageCount { get; set; }

        public int? AgeMin { get; set; }

        public int? AgeMax { get; set; }

        public string? CoverUrl { get; set; }

        public string? DownloadUrl { get; set; }
    }
}
