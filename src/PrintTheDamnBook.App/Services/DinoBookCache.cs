using System.Diagnostics;
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

    /// <summary>False when the book had to be placed beside a folder another window holds and that folder's saved words could not be copied across.</summary>
    public bool SavedWordsKept { get; init; } = true;
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
    private const int HashLength = DinoCacheLayout.HashLength;
    private const int HashChunk = 81920;
    private const int BlankWidth = 30;
    private const int BlankHeight = 40;
    private const double BlankDpi = 96;

    private const string MutexPrefix = @"Local\PrintTheDamnBook.dino-cache.";
    private const string TempMarker = ".tmp-";
    private const string OldMarker = ".old-";
    private const string AltMarker = ".alt-";

    // One placement and one clean-up at a time across every window of every process (Dino Reader starts a new process per hand-over).
    private static readonly Mutex CacheMutex = new(false, MutexPrefix + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Root.ToUpperInvariant())))[..HashLength]);

    // Folders this process is opening or showing, with a count. The clean-up never touches one (or its .tmp-/.old- siblings); a shown book is never released.
    private static readonly Dictionary<string, int> Holds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object HoldsLock = new();

    private static readonly Regex SafeId = new(@"\A[A-Za-z0-9_-]{1,64}\z", RegexOptions.Compiled);

    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolder, CacheFolder);

    public static bool IsDinoFile(string path) => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the file and makes its cache folder; always call it off the UI thread. A finished folder (its manifest verifies) is reused without
    /// writing anything; otherwise the book is extracted beside it and renamed into place under the cross-process mutex.
    /// Throws <see cref="DinoBookException"/> for a file that is not a usable Dino book.
    /// </summary>
    public static OpenedDino Open(string path, CancellationToken cancel = default)
    {
        using var read = File.OpenRead(path);
        var book = DinoBookReader.Read(read);
        var hash = HashOf(read, cancel);
        var name = FolderNameOf(book, hash);
        var folder = Path.Combine(Root, name);
        var pictures = DinoBookReader.PlannedPictures(book);
        var names = FileNamesOf(pictures);

        Hold(name);
        var held = name;
        try
        {
            var opened = Reuse(path, book, folder, names, pictures) ?? ExtractAndPlace(path, read, book, folder, names, cancel);
            held = HoldPlacedFolder(opened, name);
            SweepIfAlone(book, name);
            return opened;
        }
        catch
        {
            Release(held);
            throw;
        }
    }

    /// <summary>When the book had to be placed at an alternate folder, this open holds that one instead of the folder it asked for; returns the held name.</summary>
    private static string HoldPlacedFolder(OpenedDino opened, string name)
    {
        var placed = Path.GetFileName(opened.Folder);
        if (!string.Equals(placed, name, StringComparison.OrdinalIgnoreCase))
        {
            Hold(placed);
            Release(name);
        }

        return placed;
    }

    private static OpenedDino Opened(string path, DinoBook book, List<string> names, DinoPictures pictures, Placement placement)
    {
        return new OpenedDino(path, placement.Folder, book, names.Select(f => Path.Combine(placement.Folder, f)).ToList(), pictures)
        {
            CreatedFolder = placement.Created,
            SavedWordsKept = placement.SavedWordsKept,
        };
    }

    /// <summary>Where a book was put: whether that folder is new, and whether the earlier saved words came with it.</summary>
    internal readonly record struct Placement(string Folder, bool Created, bool SavedWordsKept = true);

    /// <summary>The finished folder already there (its pictures may be open in another window, so nothing is written); null when it is missing or does not verify.</summary>
    private static OpenedDino? Reuse(string path, DinoBook book, string folder, List<string> names, DinoPictures pictures)
    {
        return DinoCacheLayout.Verify(folder, names) ? Opened(path, book, names, pictures, new Placement(folder, false)) : null;
    }

    private static List<string> FileNamesOf(DinoPictures pictures)
    {
        return new[] { pictures.CoverFile }.OfType<string>().Concat(pictures.PageFiles).Append(BackCoverFile).ToList();
    }

    private static void Hold(string name)
    {
        lock (HoldsLock)
        {
            Holds[name] = Holds.GetValueOrDefault(name) + 1;
        }
    }

    private static void Release(string name)
    {
        lock (HoldsLock)
        {
            if (Holds.GetValueOrDefault(name) <= 1)
            {
                Holds.Remove(name);
            }
            else
            {
                Holds[name]--;
            }
        }
    }

    private static void EnterCache()
    {
        try
        {
            CacheMutex.WaitOne();
        }
        catch (AbandonedMutexException)
        {
            // The process that held it died; the mutex is ours now and its leftovers are swept by a lone start.
        }
    }

    /// <summary>True when another PrintTheDamnBook process is running; only then can a cache folder be in use that this process does not know about.</summary>
    private static bool OtherInstanceRunning()
    {
        var self = Environment.ProcessId;
        var others = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
        try
        {
            return others.Any(p => p.Id != self);
        }
        finally
        {
            foreach (var process in others)
            {
                process.Dispose();
            }
        }
    }

    /// <summary>Lets go of the hold on a book that was on screen and has been replaced; never deletes anything (the clean-up of older versions does that later). Quick, so safe on the UI thread.</summary>
    public static void ReleaseShown(OpenedDino shown)
    {
        Release(Path.GetFileName(shown.Folder));
    }

    /// <summary>Lets go of an open nobody will see (cancelled or superseded) and removes its folder when this open created it, nothing else in this process holds it and no other instance runs. Takes the mutex, so never call it on the UI thread.</summary>
    public static void Discard(OpenedDino opened)
    {
        var name = Path.GetFileName(opened.Folder);
        Release(name);
        if (!opened.CreatedFolder)
        {
            return;
        }

        EnterCache();
        try
        {
            lock (HoldsLock)
            {
                if (!Holds.ContainsKey(name) && !OtherInstanceRunning())
                {
                    TryDelete(opened.Folder);
                }
            }
        }
        finally
        {
            CacheMutex.ReleaseMutex();
        }
    }

    /// <summary>Extracts into a sibling temp folder (no lock held), then places it under the mutex; the temp is deleted on any failure.</summary>
    private static OpenedDino ExtractAndPlace(string path, Stream read, DinoBook book, string folder, List<string> names, CancellationToken cancel)
    {
        var temp = folder + TempMarker + Guid.NewGuid().ToString("N");
        try
        {
            var pictures = DinoBookReader.ExtractPictures(read, book, temp, cancel);
            WriteBlankPicture(Path.Combine(temp, BackCoverFile));
            DinoCacheLayout.WriteManifest(temp, names);
            cancel.ThrowIfCancellationRequested();
            return Opened(path, book, names, pictures, PlaceUnderMutex(temp, folder, names));
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>
    /// Puts the finished <paramref name="temp"/> at <paramref name="folder"/>; returns where the book now is and whether that folder is new. If another process
    /// finished it first, ours is dropped and theirs is used. If a damaged folder cannot be moved aside (another window holds its files), the temp is
    /// placed at <c>&lt;folder&gt;.alt-&lt;guid&gt;</c> instead; the lone-instance clean-up removes it later.
    /// </summary>
    private static Placement PlaceUnderMutex(string temp, string folder, List<string> names)
    {
        EnterCache();
        try
        {
            if (!Directory.Exists(folder))
            {
                Directory.Move(temp, folder);
                return new Placement(folder, true);
            }

            if (DinoCacheLayout.Verify(folder, names))
            {
                return new Placement(folder, false);
            }

            return TryReplaceDamaged(temp, folder) ? new Placement(folder, false) : PlaceAlternate(temp, folder);
        }
        finally
        {
            CacheMutex.ReleaseMutex();
        }
    }

    private static bool TryReplaceDamaged(string temp, string folder)
    {
        try
        {
            ReplaceDamaged(temp, folder);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Places the book beside the folder another window holds. Carrying the saved words over is best effort: if <c>book.json</c> is locked too, the book still opens, without them.</summary>
    internal static Placement PlaceAlternate(string temp, string folder)
    {
        var alternate = folder + AltMarker + Guid.NewGuid().ToString("N");
        var kept = TryCopySavedWords(folder, temp);
        Directory.Move(temp, alternate);
        return new Placement(alternate, true, kept);
    }

    private static bool TryCopySavedWords(string folder, string temp)
    {
        try
        {
            CopySavedWords(folder, temp);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[dino-cache] saved words not copied to the alternate folder ({ex.GetType().Name})");
            DinoStoreFiles.TryDelete(BookTextStore.PathIn(temp));
            return false;
        }
    }

    private static void CopySavedWords(string folder, string temp)
    {
        var saved = BookTextStore.PathIn(folder);
        if (File.Exists(saved))
        {
            File.Copy(saved, BookTextStore.PathIn(temp), overwrite: true);
        }
    }

    /// <summary>Swaps a legacy or damaged folder for the finished temp: its saved words are copied in first, it is renamed aside (not deleted) and put back if the final rename fails.</summary>
    private static void ReplaceDamaged(string temp, string folder)
    {
        CopySavedWords(folder, temp);

        var aside = folder + OldMarker + Guid.NewGuid().ToString("N");
        Directory.Move(folder, aside);
        try
        {
            Directory.Move(temp, folder);
        }
        catch
        {
            Directory.Move(aside, folder);
            throw;
        }
    }

    /// <summary>Removes older versions of this book and dead <c>.tmp-</c>/<c>.old-</c> leftovers, only under the mutex and only when no other instance runs (it could be showing or extracting any of them); otherwise the next lone start does it.</summary>
    private static void SweepIfAlone(DinoBook book, string currentName)
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        EnterCache();
        try
        {
            if (!OtherInstanceRunning())
            {
                Sweep(book, currentName);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A clean-up that cannot finish is only wasted space; never fail an open for it.
        }
        finally
        {
            CacheMutex.ReleaseMutex();
        }
    }

    private static void Sweep(DinoBook book, string currentName)
    {
        var safe = SafeId.IsMatch(book.Id);
        lock (HoldsLock)
        {
            foreach (var folder in Directory.EnumerateDirectories(Root))
            {
                var name = Path.GetFileName(folder);
                var removable = DinoCacheLayout.IsLeftover(name) || (safe && DinoCacheLayout.IsOlderVersion(name, book.Id, currentName));
                if (removable && !IsHeldHere(name))
                {
                    TryDelete(folder);
                }
            }
        }
    }

    /// <summary>True when this process holds <paramref name="name"/> or the folder a <c>.tmp-</c>/<c>.old-</c> leftover name belongs to. Call with <see cref="HoldsLock"/> held.</summary>
    private static bool IsHeldHere(string name)
    {
        return Holds.Keys.Any(held => string.Equals(held, name, StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(held + TempMarker, StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(held + OldMarker, StringComparison.OrdinalIgnoreCase));
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
