using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.App.Services;

/// <summary>A Dino book opened from a <c>.dino</c> file: its cache folder, what was read from it and the picture files to order.</summary>
public sealed record OpenedDino(string SourcePath, string Folder, DinoBook Book, IReadOnlyList<string> Files, DinoPictures Pictures)
{
    /// <summary>True when this open created the cache folder (it did not exist before), so discarding the open may remove it.</summary>
    public bool CreatedFolder { get; init; }
}

/// <summary>
/// Opens a <c>.dino</c> file into a per-book cache folder under <c>%LOCALAPPDATA%\PrintTheDamnBook\dino\&lt;id&gt;\</c>: the pictures are
/// extracted there (the file is never touched) together with a blank back cover, and the text edits are saved there in <c>book.json</c>,
/// so opening the same book again keeps them.
/// </summary>
public static class DinoBookCache
{
    public const string Extension = ".dino";
    public const string BackCoverFile = "back cover.png";

    private const string CacheFolder = "dino";
    private const string AppFolder = "PrintTheDamnBook";
    private const int HashLength = 16;
    private const int HashChunk = 81920;
    private const int BlankWidth = 30;
    private const int BlankHeight = 40;
    private const double BlankDpi = 96;

    private static readonly Lock ExtractionLock = new();

    private static readonly Regex SafeId = new(@"\A[A-Za-z0-9_-]{1,64}\z", RegexOptions.Compiled);

    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolder, CacheFolder);

    public static bool IsDinoFile(string path) => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads the file and extracts its pictures; safe to call off the UI thread. Throws <see cref="DinoBookException"/> for a file that is not a usable Dino book.</summary>
    public static OpenedDino Open(string path, CancellationToken cancel = default)
    {
        using var read = File.OpenRead(path);
        var book = DinoBookReader.Read(read);
        var hash = HashOf(read, cancel);
        var folder = Path.Combine(Root, FolderNameOf(book, hash));

        // One extraction at a time: a cancelled open and a quick re-open of the same file never write the same folder together.
        lock (ExtractionLock)
        {
            cancel.ThrowIfCancellationRequested();
            var opened = ExtractInto(path, read, book, folder, cancel);
            DeleteOlderVersions(book, hash);
            return opened;
        }
    }

    /// <summary>Removes the cache folder of an open nobody will see (cancelled after it finished); a folder that existed before the open is kept.</summary>
    public static void Discard(OpenedDino opened)
    {
        if (opened.CreatedFolder)
        {
            lock (ExtractionLock)
            {
                TryDelete(opened.Folder);
            }
        }
    }

    /// <summary>Extracts into <paramref name="folder"/>; a folder this call created is removed again if the open fails or is cancelled.</summary>
    private static OpenedDino ExtractInto(string path, Stream read, DinoBook book, string folder, CancellationToken cancel)
    {
        var isNew = !Directory.Exists(folder);
        try
        {
            var pictures = DinoBookReader.ExtractPictures(read, book, folder, cancel);
            WriteBlankPicture(Path.Combine(folder, BackCoverFile));
            var files = new[] { pictures.CoverFile }.OfType<string>().Concat(pictures.PageFiles).Append(BackCoverFile).Select(f => Path.Combine(folder, f)).ToList();
            return new OpenedDino(path, folder, book, files, pictures) { CreatedFolder = isNew };
        }
        catch
        {
            if (isNew)
            {
                TryDelete(folder);
            }

            throw;
        }
    }

    /// <summary>Removes the folders of older versions of this book (same id, another hash); never another book's, and never one with an unsafe id.</summary>
    private static void DeleteOlderVersions(DinoBook book, string hash)
    {
        if (!SafeId.IsMatch(book.Id) || !Directory.Exists(Root))
        {
            return;
        }

        var current = FolderNameOf(book, hash);
        var older = new Regex(@"\A" + Regex.Escape(book.Id) + "-[0-9a-f]{" + HashLength + @"}\z", RegexOptions.IgnoreCase);
        foreach (var folder in Directory.EnumerateDirectories(Root))
        {
            var name = Path.GetFileName(folder);
            if (older.IsMatch(name) && !string.Equals(name, current, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(folder);
            }
        }
    }

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover folder is only wasted space; never fail an open for it.
        }
    }

    /// <summary>A short hash of the file's bytes, so a re-exported book (same id, new words or pictures) never reuses the folder of an older one. Leaves the stream at the start.</summary>
    private static string HashOf(Stream file, CancellationToken cancel)
    {
        file.Position = 0;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[HashChunk];
        int read;
        while ((read = file.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancel.ThrowIfCancellationRequested();
            sha.AppendData(buffer, 0, read);
        }

        file.Position = 0;
        return Convert.ToHexString(sha.GetHashAndReset())[..HashLength].ToLowerInvariant();
    }

    /// <summary>The book's id plus the file's hash when the id is plain enough to be a folder name, else the hash alone.</summary>
    private static string FolderNameOf(DinoBook book, string hash)
    {
        return SafeId.IsMatch(book.Id) ? book.Id + "-" + hash : hash;
    }

    /// <summary>A Dino book has no back cover, so the book ends on a plain white page that prints as nothing. Written to a temp file and moved, so a crash never leaves a half-written one.</summary>
    private static void WriteBlankPicture(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        var pixels = new byte[BlankWidth * BlankHeight];
        Array.Fill(pixels, (byte)255);
        var bitmap = BitmapSource.Create(BlankWidth, BlankHeight, BlankDpi, BlankDpi, PixelFormats.Gray8, null, pixels, BlankWidth);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = File.Create(temp))
            {
                encoder.Save(file);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}

/// <summary>The open-file dialog for a Dino book.</summary>
public static class DinoFilePicker
{
    private const string Filter = "Dino books (*.dino)|*.dino";

    public static string? Pick()
    {
        var dialog = new OpenFileDialog { Title = "Choose a Dino book", Filter = Filter, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
