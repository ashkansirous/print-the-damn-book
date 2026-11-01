using System.Text.RegularExpressions;

namespace PrintTheDamnBook.Core.Dino;

/// <summary>How far a download has come; <see cref="Total"/> is null when the store did not say.</summary>
public readonly record struct DownloadProgress(long Received, long? Total);

/// <summary>The limits of one capped copy: the most bytes to take, the length the server claimed, and how long to wait for the next byte.</summary>
public readonly record struct CopyLimits(long Cap, long? Total, TimeSpan Stall, string TooLargeMessage = DinoStreamCopier.TooLarge);

/// <summary>A failure on this PC's own disk (full, read-only, locked) while saving store files, as opposed to a network failure.</summary>
public sealed class DinoDiskException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The finished file for this book is there with different bytes (or locked) because another window holds it; not a disk problem.</summary>
public sealed class DinoFileInUseException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>A failure while reading the store's response stream (a reset or dropped connection). Raised where the stream is read, so nothing downstream has to guess whether a bare <see cref="IOException"/> came from the network or the disk.</summary>
public sealed class DinoNetworkException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The size and stall rules for a store transfer (Slice 18). The server's <c>Content-Length</c> is only a hint, so bytes are counted too, and a
/// stream that goes quiet is given up on instead of waiting forever.
/// </summary>
public static class DinoStreamCopier
{
    public const string TooLarge = "That book is too large.";
    public const string CutShort = "The download was cut short.";
    public const string TimedOut = "Dino's store took too long to answer.";
    public const string DiskMessage = "Could not save the book on this PC.";
    public const string InUseMessage = "A copy of this book is in use by another window. Close that window and try again.";

    private const int CopyChunk = 81920;

    /// <summary>Throws <see cref="DinoBookException"/> when the server already says the body is over <paramref name="cap"/>.</summary>
    public static void CheckLength(long? contentLength, long cap, string message = TooLarge)
    {
        if (contentLength > cap)
        {
            throw new DinoBookException(message);
        }
    }

    /// <summary>
    /// Copies at most <see cref="CopyLimits.Cap"/> bytes (counted, since a server can lie about its length) and gives up when no byte arrives
    /// for <see cref="CopyLimits.Stall"/>. A failure writing to <paramref name="target"/> is a <see cref="DinoDiskException"/>.
    /// </summary>
    public static async Task CopyAsync(Stream source, Stream target, CopyLimits limits, IProgress<DownloadProgress> progress, CancellationToken cancel)
    {
        var buffer = new byte[CopyChunk];
        long received = 0;
        while (true)
        {
            var read = await ReadWithStallLimitAsync(source, buffer, limits.Stall, cancel);
            if (read == 0)
            {
                if (limits.Total is { } expected && received != expected)
                {
                    throw new DinoBookException(CutShort);
                }

                await OnDiskAsync(() => target.FlushAsync(cancel));
                return;
            }

            received += read;
            if (received > limits.Cap)
            {
                throw new DinoBookException(limits.TooLargeMessage);
            }

            await OnDiskAsync(() => target.WriteAsync(buffer.AsMemory(0, read), cancel).AsTask());
            progress.Report(new DownloadProgress(received, limits.Total));
        }
    }

    private static async Task<int> ReadWithStallLimitAsync(Stream source, byte[] buffer, TimeSpan stall, CancellationToken cancel)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(stall);
        try
        {
            return await source.ReadAsync(buffer, limit.Token);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            throw new DinoBookException(TimedOut);
        }
        catch (IOException ex)
        {
            throw new DinoNetworkException(CutShort, ex);
        }
    }

    private static async Task OnDiskAsync(Func<Task> write)
    {
        try
        {
            await write();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DinoDiskException(DiskMessage, ex);
        }
    }
}

/// <summary>
/// Where the store's files live and how they are written (Slice 18): a file is written to <c>&lt;name&gt;.part</c> and renamed when whole, so a
/// cancelled or failed write never leaves a truncated file that a later run would trust.
/// </summary>
public static class DinoStoreFiles
{
    public const string DinoExtension = ".dino";
    public const string CoverFolder = "covers";
    public const string PartSuffix = ".part";

    private const string CoverExtension = ".jpg";
    private const int FileBuffer = 81920;

    private static readonly Regex NameOfCoverVersion = new(@"\A(?<id>.+)-v(?<version>[0-9]+)" + Regex.Escape(CoverExtension) + @"\z", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NameOfVersion = new(@"\A(?<id>.+)-v(?<version>[0-9]+)\.dino\z", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The finished download of one version of a book: <c>&lt;root&gt;\&lt;id&gt;-v&lt;version&gt;.dino</c>.</summary>
    public static string FinalPath(string root, DinoStoreEntry entry)
    {
        return Path.Combine(root, $"{entry.Id}-v{entry.Version}{DinoExtension}");
    }

    /// <summary>The cached cover of one version of a book.</summary>
    public static string CoverPath(string root, DinoStoreEntry entry)
    {
        return Path.Combine(root, CoverFolder, $"{entry.Id}-v{entry.Version}{CoverExtension}");
    }

    /// <summary>The file a write goes to until it is whole.</summary>
    public static string PartPath(string finalPath)
    {
        return finalPath + PartSuffix;
    }

    /// <summary>The older versions of the same book as <paramref name="keepPath"/>, among <paramref name="files"/>; the file to keep and any newer version are never listed.</summary>
    public static IReadOnlyList<string> OtherVersions(IEnumerable<string> files, string keepPath)
    {
        var keep = NameOfVersion.Match(Path.GetFileName(keepPath));
        if (!keep.Success || !long.TryParse(keep.Groups["version"].Value, out var keepVersion))
        {
            return [];
        }

        var id = keep.Groups["id"].Value;
        return files.Where(f => !string.Equals(f, keepPath, StringComparison.OrdinalIgnoreCase) && IsOlderVersionOf(Path.GetFileName(f), id, keepVersion)).ToList();
    }

    private static bool IsOlderVersionOf(string fileName, string id, long keepVersion)
    {
        var match = NameOfVersion.Match(fileName);
        return match.Success
            && string.Equals(match.Groups["id"].Value, id, StringComparison.OrdinalIgnoreCase)
            && long.TryParse(match.Groups["version"].Value, out var version)
            && version < keepVersion;
    }

    /// <summary>
    /// Runs <paramref name="write"/> against a uniquely named <c>.part</c> file (so two windows never share one), then renames it to <paramref name="finalPath"/>. On any failure or cancel the
    /// part file is removed and the final file is untouched. A disk problem is a <see cref="DinoDiskException"/>; <paramref name="cancel"/> stops the final comparison.
    /// </summary>
    public static async Task WriteAtomicallyAsync(string finalPath, Func<Stream, Task> write, CancellationToken cancel = default)
    {
        var part = PartPath(finalPath + "." + Guid.NewGuid().ToString("N"));
        FileStream? target = null;
        try
        {
            target = OpenPart(part);
            await write(target);
            await CloseOnDiskAsync(target);
            await MoveIntoPlaceAsync(part, finalPath, cancel);
        }
        catch
        {
            await DisposeQuietlyAsync(target);
            TryDelete(part);
            throw;
        }
    }

    /// <summary>The cached covers of the older versions of the same book as <paramref name="keepPath"/>, among <paramref name="files"/>; a newer version (another window's) is never listed.</summary>
    public static IReadOnlyList<string> OtherCoverVersions(IEnumerable<string> files, string keepPath)
    {
        var match = NameOfCoverVersion.Match(Path.GetFileName(keepPath));
        if (!match.Success)
        {
            return [];
        }

        if (!long.TryParse(match.Groups["version"].Value, out var keepVersion))
        {
            return [];
        }

        var id = match.Groups["id"].Value;
        return files.Where(f => !string.Equals(f, keepPath, StringComparison.OrdinalIgnoreCase) && IsOlderCoverOf(Path.GetFileName(f), id, keepVersion)).ToList();
    }

    private static bool IsOlderCoverOf(string fileName, string id, long keepVersion)
    {
        var match = NameOfCoverVersion.Match(fileName);
        return match.Success
            && string.Equals(match.Groups["id"].Value, id, StringComparison.OrdinalIgnoreCase)
            && long.TryParse(match.Groups["version"].Value, out var version)
            && version < keepVersion;
    }

    /// <summary>The leftover <c>.part</c> files among <paramref name="files"/> (a crashed or killed write), for sweeping when nothing is writing.</summary>
    public static IReadOnlyList<string> StalePartFiles(IEnumerable<string> files)
    {
        return files.Where(f => f.EndsWith(PartSuffix, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Flushes and closes the part file; the flush on close is where a full disk shows up, so it is a disk failure too.</summary>
    private static async Task CloseOnDiskAsync(FileStream target)
    {
        try
        {
            await target.DisposeAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DinoDiskException(DinoStreamCopier.DiskMessage, ex);
        }
    }

    private static async Task DisposeQuietlyAsync(FileStream? target)
    {
        if (target is null)
        {
            return;
        }

        try
        {
            await target.DisposeAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The part file is being thrown away anyway.
        }
    }

    private static bool SameBytes(string first, string second, CancellationToken cancel)
    {
        try
        {
            using var a = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var b = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return a.Length == b.Length && StreamsEqual(a, b, cancel);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool StreamsEqual(Stream a, Stream b, CancellationToken cancel)
    {
        var left = new byte[FileBuffer];
        var right = new byte[FileBuffer];
        int read;
        while (!cancel.IsCancellationRequested && (read = a.ReadAtLeast(left, left.Length, throwOnEndOfStream: false)) > 0)
        {
            if (b.ReadAtLeast(right, read, throwOnEndOfStream: false) != read || !left.AsSpan(0, read).SequenceEqual(right.AsSpan(0, read)))
            {
                return false;
            }
        }

        cancel.ThrowIfCancellationRequested();
        return true;
    }

    /// <summary>Deletes a file, ignoring a lock or a missing permission: a leftover file is only wasted space.</summary>
    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Never fail a download for a file that could not be cleaned up.
        }
    }

    private static FileStream OpenPart(string part)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(part)!);
            return new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, FileBuffer, useAsync: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DinoDiskException(DinoStreamCopier.DiskMessage, ex);
        }
    }

    /// <summary>Renames the whole part file into place. A window that finished the same book first leaves the file there (and maybe open); if it holds the same bytes it is as good as our own: the part is dropped and it counts as success (compared off the caller's thread, length first, and it stops on cancel). A different file in the way (damaged, stale) is a failure.</summary>
    private static async Task MoveIntoPlaceAsync(string part, string finalPath, CancellationToken cancel)
    {
        try
        {
            File.Move(part, finalPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!await Task.Run(() => SameBytes(part, finalPath, cancel), cancel))
            {
                throw new DinoFileInUseException(DinoStreamCopier.InUseMessage, ex);
            }

            TryDelete(part);
        }
    }
}
