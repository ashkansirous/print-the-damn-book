using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrintTheDamnBook.Core.Text;

/// <summary>The words of one picture as saved in <c>book.json</c>.</summary>
public sealed record PageTextEntry(string Text = "", TextPlacement Placement = TextPlacement.OnPicture, TextBand? Band = null);

/// <summary>What <c>book.json</c> holds: the story text of each picture (keyed by image file name) and the book's text-page paper.</summary>
public sealed record BookText
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "book.json";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public TextPageBackground TextPages { get; init; } = TextPageBackground.White;

    /// <summary>For a Dino book, the language its words are in (null for the book's own); unused for a folder.</summary>
    public string? Language { get; init; }

    public Dictionary<string, PageTextEntry> Pages { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>The result of reading <c>book.json</c>: the book and whether it is safe to write the file back (false for a file from a newer version or one that could not be read).</summary>
public sealed record BookTextLoad(BookText Book, bool CanSave);

/// <summary>Reads and writes <see cref="BookText"/>. Reading never throws; a missing or broken file reads as no text.</summary>
public static class BookTextJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(BookText book)
    {
        return JsonSerializer.Serialize(book, Options);
    }

    public static BookTextLoad Parse(string? json)
    {
        try
        {
            var parsed = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<BookText>(json, Options);
            if (parsed is null)
            {
                return new BookTextLoad(new BookText(), true);
            }

            return parsed.SchemaVersion > BookText.CurrentSchemaVersion
                ? new BookTextLoad(new BookText(), false)
                : new BookTextLoad(Normalize(parsed), true);
        }
        catch (JsonException)
        {
            // Malformed or unknown values: read as no text, but never overwrite what the user may have hand-edited.
            return new BookTextLoad(new BookText(), false);
        }
    }

    /// <summary>
    /// The <c>book.json</c> key of a page's picture: the plain file name for a picture in the book folder, the full path for one added
    /// from elsewhere, so two pictures with the same name in different folders never share an entry.
    /// </summary>
    public static string KeyFor(string folder, string imagePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(imagePath));
        var book = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        return string.Equals(directory, book, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(imagePath)
            : Path.GetFullPath(imagePath);
    }

    public static BookTextLoad Load(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new BookTextLoad(new BookText(), true);
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            // Unreadable (locked, no permission): read as no text, but never overwrite what may be a good file.
            return new BookTextLoad(new BookText(), false);
        }
    }

    /// <summary>Writes through a unique temporary file so a crash never leaves half a file. Returns false when the file could not be written.</summary>
    public static bool TrySave(string path, BookText book)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, Serialize(book));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            TryDelete(temp);
            return false;
        }
    }

    private static bool IsFileFailure(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            // The stray temp file is harmless.
        }
    }

    /// <summary>Repairs values that parsed but make no sense: null entries, empty text, out-of-range enums.</summary>
    private static BookText Normalize(BookText book)
    {
        var pages = new Dictionary<string, PageTextEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, entry) in book.Pages ?? [])
        {
            if (entry is { Text: { } text } && !string.IsNullOrWhiteSpace(text))
            {
                pages[name] = entry with
                {
                    Placement = Enum.IsDefined(entry.Placement) ? entry.Placement : TextPlacement.OnPicture,
                    Band = entry.Band is { } band && Enum.IsDefined(band) ? band : null,
                };
            }
        }

        return book with
        {
            Pages = pages,
            TextPages = Enum.IsDefined(book.TextPages) ? book.TextPages : TextPageBackground.White,
        };
    }
}
