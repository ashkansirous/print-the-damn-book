using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace PrintTheDamnBook.Core.Dino;

/// <summary>The picture files <see cref="DinoBookReader.ExtractPictures"/> wrote: the cover (if any) and one per page, in page order.</summary>
public sealed record DinoPictures(string? CoverFile, IReadOnlyList<string> PageFiles);

/// <summary>
/// Reads a <c>.dino</c> package (a zip: <c>package.json</c>, <c>story.json</c> and the pictures). The file is untrusted: every entry name must be a
/// plain file name (no folders, no <c>..</c>, no drive letters), sizes are capped (also while copying, since a zip can lie about its sizes),
/// and a package from a newer schema is refused. The format is the one Dino Reader writes (ReadTheStupidText <c>StoryPackageReader</c>).
/// </summary>
public static class DinoBookReader
{
    public const int SupportedSchemaVersion = 1;
    public const int MaxEntries = 2000;
    public const long MaxManifestBytes = 64 * 1024;
    public const long MaxStoryBytes = 8 * 1024 * 1024;
    public const long MaxPictureBytes = 25 * 1024 * 1024;
    public const long MaxTotalBytes = 200L * 1024 * 1024;
    public const int MaxPages = 500;
    public const int MaxPageChars = 20_000;
    public const int MaxTranslationEntries = 5000;
    public const int MaxTranslationKeyChars = 1000;

    public const string ManifestName = "package.json";
    public const string StoryName = "story.json";

    private const string MacMetadataFolder = "__MACOSX/";
    private const string DamagedZip = "This is not a valid Dino book (the zip file is damaged).";
    private const string TooLarge = "This Dino book is too large.";
    private const string PictureStyleName = "Picture";
    private const int PictureStyleNumber = 2;
    private static readonly char[] SeparatorChars = ['/', '\\', ':'];
    private static readonly string[] PictureExtensions = [".jpg", ".jpeg", ".png"];
    private static readonly HashSet<string> DeviceNames = new(["CON", "PRN", "AUX", "NUL"], StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static DinoBook Read(Stream package)
    {
        try
        {
            using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            var entries = IndexEntries(zip);
            var manifest = ParseManifest(ReadText(entries, ManifestName));
            var story = Parse<StoryFile>(ReadText(entries, StoryName), StoryName);
            return Build(manifest, story, entries);
        }
        catch (InvalidDataException ex)
        {
            throw new DinoBookException(DamagedZip, ex);
        }
    }

    /// <summary>
    /// Writes the cover and page pictures of <paramref name="book"/> into <paramref name="folder"/> as <c>cover.jpg</c>, <c>1.jpg</c>, <c>2.jpg</c> and so on,
    /// which the folder ordering understands. A picture the book uses twice is inflated once and copied; everything written counts against <see cref="MaxTotalBytes"/>.
    /// </summary>
    public static DinoPictures ExtractPictures(Stream package, DinoBook book, string folder, CancellationToken cancel = default)
    {
        try
        {
            using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            var writer = new PictureWriter(IndexEntries(zip), folder, cancel);
            Directory.CreateDirectory(folder);
            var cover = book.CoverFile is { } c ? writer.Write(c, "cover") : null;
            var pages = book.Pages.Select((page, i) => writer.Write(page.ImageFile, (i + 1).ToString(CultureInfo.InvariantCulture))).ToList();
            return new DinoPictures(cover, pages);
        }
        catch (InvalidDataException ex)
        {
            throw new DinoBookException(DamagedZip, ex);
        }
        catch (KeyNotFoundException ex)
        {
            throw new DinoBookException("The book does not match the file that was read.", ex);
        }
    }

    private static DinoBook Build(ManifestFile manifest, StoryFile story, Dictionary<string, ZipArchiveEntry> entries)
    {
        var pages = new List<DinoPage>();
        var skipped = 0;
        if ((story.Pages?.Count ?? 0) > MaxPages)
        {
            throw new DinoBookException("This Dino book has too many pages.");
        }

        foreach (var page in story.Pages ?? [])
        {
            if (page is not null && IsPicture(page.ImageFile, entries))
            {
                pages.Add(new DinoPage(CheckedText(page.Text), page.ImageFile!));
            }
            else
            {
                skipped++;
            }
        }

        if (pages.Count == 0)
        {
            throw new DinoBookException("This Dino book has no pages with pictures.");
        }

        var title = FirstText(story.Title, manifest.Title) ?? string.Empty;
        var cover = CoverOf(story, entries);
        var id = FirstText(manifest.Id, story.Id) ?? string.Empty;
        return new DinoBook(id, title, FirstText(story.Language, manifest.Language) ?? "en", cover, pages, ReadTranslations(story.Translations), skipped);
    }

    private static string CheckedText(string? text)
    {
        return text is { Length: > MaxPageChars } ? throw new DinoBookException("A page of this Dino book has too many words.") : text ?? string.Empty;
    }

    /// <summary>The cover picture: the chosen page's picture when the cover is "a page's picture", else the cover file; null when there is none.</summary>
    private static string? CoverOf(StoryFile story, Dictionary<string, ZipArchiveEntry> entries)
    {
        var name = story.CoverFile;
        if (IsPictureStyle(story.CoverStyle) && story.CoverPageIndex is { } index && story.Pages is { } pages && index >= 0 && index < pages.Count)
        {
            name = pages[index]?.ImageFile;
        }

        return IsPicture(name, entries) ? name : null;
    }

    private static bool IsPictureStyle(JsonElement? style) => style switch
    {
        { ValueKind: JsonValueKind.String } s => string.Equals(s.GetString(), PictureStyleName, StringComparison.OrdinalIgnoreCase),
        { ValueKind: JsonValueKind.Number } n => n.TryGetInt32(out var number) && number == PictureStyleNumber,
        _ => false,
    };

    private static string? FirstText(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static bool IsPicture(string? name, Dictionary<string, ZipArchiveEntry> entries) =>
        name is not null && entries.ContainsKey(name) && PictureExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    /// <summary>Keeps the tables that are objects of strings; anything else in <c>Translations</c> is ignored rather than failing the book.</summary>
    private static Dictionary<string, IReadOnlyDictionary<string, string>> ReadTranslations(JsonElement? element)
    {
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (element is not { ValueKind: JsonValueKind.Object } all)
        {
            return tables;
        }

        var budget = MaxTranslationEntries;
        foreach (var language in all.EnumerateObject().Where(l => l.Value.ValueKind == JsonValueKind.Object))
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var sentence in language.Value.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String && p.Name.Length <= MaxTranslationKeyChars))
            {
                if (--budget < 0)
                {
                    throw new DinoBookException(TooLarge);
                }

                table[sentence.Name] = sentence.Value.GetString()!;
            }

            if (table.Count > 0)
            {
                tables[language.Name] = table;
            }
        }

        return tables;
    }

    private static ManifestFile ParseManifest(string json)
    {
        var manifest = Parse<ManifestFile>(json, ManifestName);
        return manifest.SchemaVersion > SupportedSchemaVersion
            ? throw new DinoBookException("This Dino book is from a newer version of Dino Reader than I understand. Update Print the Damn Book and try again.")
            : manifest;
    }

    private static T Parse<T>(string json, string name)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? throw new DinoBookException($"{name} in this Dino book is empty.");
        }
        catch (JsonException ex)
        {
            throw new DinoBookException($"{name} in this Dino book cannot be read.", ex);
        }
    }

    private static Dictionary<string, ZipArchiveEntry> IndexEntries(ZipArchive zip)
    {
        if (zip.Entries.Count > MaxEntries)
        {
            throw new DinoBookException("This Dino book has too many files.");
        }

        long total = 0;
        var index = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries.Where(e => !IsIgnorable(e)))
        {
            if (!IsPlainName(entry.FullName))
            {
                throw new DinoBookException("This Dino book has a file name that is not allowed, so I did not open it.");
            }

            total += entry.Length;
            if (total > MaxTotalBytes || entry.Length > MaxOf(entry.FullName))
            {
                throw new DinoBookException(TooLarge);
            }

            if (!index.TryAdd(entry.FullName, entry))
            {
                throw new DinoBookException("This Dino book has two files with the same name.");
            }
        }

        return index;
    }

    private static bool IsIgnorable(ZipArchiveEntry entry) =>
        (entry.FullName.EndsWith('/') && entry.Length == 0) || entry.FullName.StartsWith(MacMetadataFolder, StringComparison.Ordinal);

    private static bool IsPlainName(string name) =>
        name.Length > 0 && name is not ("." or "..") && name == Path.GetFileName(name) && name.IndexOfAny(SeparatorChars) < 0 && !IsDeviceName(name);

    /// <summary>CON, NUL, COM1 and friends are device names on Windows whatever their extension.</summary>
    private static bool IsDeviceName(string name)
    {
        var stem = name.Split('.')[0].TrimEnd(' ');
        return DeviceNames.Contains(stem) || (stem.Length == 4 && stem[..3].ToUpperInvariant() is "COM" or "LPT" && char.IsDigit(stem[3]));
    }

    private static long MaxOf(string name) => name.ToLowerInvariant() switch
    {
        ManifestName => MaxManifestBytes,
        StoryName => MaxStoryBytes,
        _ => MaxPictureBytes,
    };

    private static string ReadText(Dictionary<string, ZipArchiveEntry> entries, string name)
    {
        if (!entries.TryGetValue(name, out var entry))
        {
            throw new DinoBookException($"This is not a Dino book: it has no {name}.");
        }

        using var memory = new MemoryStream();
        CopyCapped(entry, memory, MaxOf(name));
        return Encoding.UTF8.GetString(memory.ToArray()).TrimStart('﻿');
    }

    /// <summary>Copies an entry, stopping at <paramref name="max"/> bytes however large the zip says it is; <paramref name="charge"/> is told every chunk written.</summary>
    private static void CopyCapped(ZipArchiveEntry entry, Stream destination, long max, Action<int>? charge = null, CancellationToken cancel = default)
    {
        using var source = entry.Open();
        var buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancel.ThrowIfCancellationRequested();
            copied += read;
            if (copied > max)
            {
                throw new DinoBookException(TooLarge);
            }

            charge?.Invoke(read);
            destination.Write(buffer, 0, read);
        }
    }

    /// <summary>Writes the pictures of one extraction, keeping the bytes written under <see cref="MaxTotalBytes"/> and inflating each entry once.</summary>
    private sealed class PictureWriter(Dictionary<string, ZipArchiveEntry> entries, string folder, CancellationToken cancel)
    {
        private readonly Dictionary<string, string> _firstWrite = new(StringComparer.OrdinalIgnoreCase);
        private long _remaining = MaxTotalBytes;

        public string Write(string entryName, string stem)
        {
            var entry = entries[entryName];
            var fileName = stem + Path.GetExtension(entry.FullName).ToLowerInvariant();
            var target = Path.Combine(folder, fileName);
            if (_firstWrite.TryGetValue(entryName, out var first))
            {
                CopyFrom(first, target);
            }
            else
            {
                Inflate(entry, target);
                _firstWrite[entryName] = target;
            }

            return fileName;
        }

        private void Inflate(ZipArchiveEntry entry, string target)
        {
            if (File.Exists(target) && SameContent(entry, target))
            {
                Charge(new FileInfo(target).Length);
                return;
            }

            var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = File.Create(temp))
                {
                    CopyCapped(entry, file, MaxPictureBytes, bytes => Charge(bytes), cancel);
                }

                File.Move(temp, target, overwrite: true);
            }
            finally
            {
                File.Delete(temp);
            }
        }

        private void CopyFrom(string source, string target)
        {
            Charge(new FileInfo(source).Length);
            cancel.ThrowIfCancellationRequested();
            File.Copy(source, target, overwrite: true);
        }

        private void Charge(long bytes)
        {
            _remaining -= bytes;
            if (_remaining < 0)
            {
                throw new DinoBookException(TooLarge);
            }
        }

        /// <summary>True when the file already holds exactly what the entry inflates to (a cached picture of the same length is not enough).</summary>
        private bool SameContent(ZipArchiveEntry entry, string path)
        {
            using var file = File.OpenRead(path);
            if (file.Length != entry.Length)
            {
                return false;
            }

            using var source = entry.Open();
            var fromZip = new byte[81920];
            var fromDisk = new byte[81920];
            int count;
            while ((count = source.ReadAtLeast(fromZip, fromZip.Length, throwOnEndOfStream: false)) > 0)
            {
                cancel.ThrowIfCancellationRequested();
                var onDisk = file.ReadAtLeast(fromDisk.AsSpan(0, count), count, throwOnEndOfStream: false);
                if (onDisk != count || !fromZip.AsSpan(0, count).SequenceEqual(fromDisk.AsSpan(0, count)))
                {
                    return false;
                }
            }

            return file.Position == file.Length;
        }
    }

    private sealed class ManifestFile
    {
        public int SchemaVersion { get; set; }

        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Language { get; set; }
    }

    private sealed class StoryFile
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Language { get; set; }

        public string? CoverFile { get; set; }

        public JsonElement? CoverStyle { get; set; }

        public int? CoverPageIndex { get; set; }

        public List<PageFile?>? Pages { get; set; }

        public JsonElement? Translations { get; set; }
    }

    private sealed class PageFile
    {
        public string? Text { get; set; }

        public string? ImageFile { get; set; }
    }
}
