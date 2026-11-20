using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp.Cover;

namespace PrintTheDamnBook.Kdp;

/// <summary>One other book's front cover for the last page, with its title for the person reading the command's output.</summary>
public sealed record KdpOtherBookTile(string Title, byte[] Jpeg);

/// <summary>The tiles chosen for a book's last page and what to tell the person about the books that could not be used.</summary>
public sealed record KdpOtherBooksResult(IReadOnlyList<KdpOtherBookTile> Tiles, IReadOnlyList<string> Warnings);

/// <summary>
/// Finds the author's other books (Slice 41): the <c>.dino</c> files of a folder, this book left out, picked and ordered by <see cref="KdpOtherBooks"/> (at most 15,
/// seeded by this book's id), each drawn as its own front cover from its own <c>kdp.json</c> (found by book id under the folder that holds every book's output
/// folder). A book that cannot be read or drawn is skipped with a warning and the next one in the order takes its place.
/// </summary>
internal static class KdpOtherBooksLoader
{
    private const string DinoPattern = "*.dino";

    private sealed record Candidate(string Path, DinoBook Book);

    /// <summary>One output folder's <c>kdp.json</c>: its folder name (output folders are named <c>&lt;book&gt;-&lt;language&gt;</c>) and what it holds.</summary>
    private sealed record FolderSettings(string FolderName, KdpBookSettings Settings);

    public static KdpOtherBooksResult Load(string dinoFolder, string outputsRoot, string thisBookId, string language)
    {
        var warnings = new List<string>();
        var ordered = KdpOtherBooks.Order(thisBookId, Candidates(dinoFolder, warnings), c => c.Book.Id);
        var settings = SettingsById(outputsRoot);
        var tiles = new List<KdpOtherBookTile>();
        foreach (var candidate in ordered.TakeWhile(_ => tiles.Count < KdpOtherBooks.MaxBooks))
        {
            if (TryTile(candidate, SettingsFor(settings, candidate.Book.Id, language), language, warnings) is { } tile)
            {
                tiles.Add(tile);
            }
        }

        if (tiles.Count == 0)
        {
            warnings.Add($"other books: no other book could be used from \"{dinoFolder}\"; the last page shows the QR code and line only");
        }

        return new KdpOtherBooksResult(tiles, warnings);
    }

    /// <summary>The <c>kdp.json</c> to draw a book's tile with: of its output folders the one made for <paramref name="language"/>, else the first by folder name, so a rebuild picks the same one.</summary>
    private static KdpBookSettings SettingsFor(Dictionary<string, List<FolderSettings>> byId, string bookId, string language)
    {
        if (!byId.TryGetValue(bookId, out var folders))
        {
            return KdpBookSettings.Empty;
        }

        var suffix = "-" + DinoBook.Primary(language);
        var match = folders.FirstOrDefault(f => f.FolderName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        return (match ?? folders[0]).Settings;
    }

    /// <summary>The books of the folder, newest file first so that of two files with one book id the newest is used; a file that is not a usable book is skipped with a warning.</summary>
    private static List<Candidate> Candidates(string folder, List<string> warnings)
    {
        if (!Directory.Exists(folder))
        {
            throw new KdpException($"--other-books folder \"{folder}\" does not exist.");
        }

        var found = new List<Candidate>();
        var files = Directory.GetFiles(folder, DinoPattern, new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive });
        foreach (var path in files.OrderByDescending(File.GetLastWriteTimeUtc).ThenBy(p => p, StringComparer.Ordinal))
        {
            try
            {
                using var stream = File.OpenRead(path);
                found.Add(new Candidate(path, DinoBookReader.Read(stream)));
            }
            catch (Exception ex) when (ex is DinoBookException or IOException or UnauthorizedAccessException)
            {
                warnings.Add($"other books: {System.IO.Path.GetFileName(path)} was skipped ({ex.Message})");
            }
        }

        return found;
    }

    /// <summary>Every <c>kdp.json</c> one folder below the outputs root, by the id of the book it was made for; one that cannot be read is ignored (that book's cover then uses the fallback rule).</summary>
    private static Dictionary<string, List<FolderSettings>> SettingsById(string root)
    {
        var byId = new Dictionary<string, List<FolderSettings>>(StringComparer.Ordinal);
        foreach (var folder in OutputFolders(root))
        {
            try
            {
                var settings = KdpBookSettings.Read(System.IO.Path.Combine(folder, KdpBookSettings.FileName));
                if (settings.BookId is { } id)
                {
                    byId.TryAdd(id, []);
                    byId[id].Add(new FolderSettings(System.IO.Path.GetFileName(folder), settings));
                }
            }
            catch (KdpSettingsException)
            {
                // Best effort: an unreadable file only means that book's cover is drawn by the fallback rule.
            }
        }

        return byId;
    }

    /// <summary>The folders one level below the outputs root in a fixed (ordinal) order; none when the root is missing or cannot be listed.</summary>
    private static List<string> OutputFolders(string root)
    {
        try
        {
            return Directory.Exists(root) ? [.. Directory.GetDirectories(root).Order(StringComparer.Ordinal)] : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static KdpOtherBookTile? TryTile(Candidate candidate, KdpBookSettings settings, string language, List<string> warnings)
    {
        try
        {
            return Tile(candidate, settings, language);
        }
        catch (Exception ex) when (ex is KdpException or DinoBookException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException
            or QuestPDF.Drawing.Exceptions.DocumentDrawingException or QuestPDF.Drawing.Exceptions.DocumentLayoutException)
        {
            warnings.Add($"other books: \"{candidate.Book.Title}\" was skipped ({ex.Message})");
            return null;
        }
    }

    /// <summary>The book's front cover as its own cover is drawn: its words in this book's language when it has it, else in its own.</summary>
    private static KdpOtherBookTile Tile(Candidate candidate, KdpBookSettings settings, string language)
    {
        var book = candidate.Book;
        var code = book.Languages().Any(l => l.IsSupported && DinoBook.Primary(l.Code) == DinoBook.Primary(language)) ? language : book.Language;
        var front = ReadFrontPicture(candidate.Path, book);
        var title = book.In(book.Title, code);
        var input = new KdpCoverInput(title, code, null, front, null, KdpPagePlan.TotalPages(book.Pages.Count), TextDirection.ForLanguage(code), settings);
        return new KdpOtherBookTile(title, KdpFrontTile.Jpeg(input));
    }

    /// <summary>Only the picture the tile uses is read out of the other book's package: its cover, else its first page.</summary>
    private static byte[] ReadFrontPicture(string path, DinoBook book)
    {
        if (book.CoverFile is null && book.Pages.Count == 0)
        {
            throw new KdpException("The book has no cover and no page picture.");
        }

        var onlyFront = book.CoverFile is null ? book with { Pages = [book.Pages[0]], BackCover = null } : book with { Pages = [], BackCover = null };
        var pictures = KdpExporter.ReadPictures(path, onlyFront);
        return pictures.Cover ?? pictures.Pages[0];
    }
}
