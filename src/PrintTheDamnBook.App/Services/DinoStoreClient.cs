using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using PrintTheDamnBook.App.ViewModels;
using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.App.Services;

/// <summary>Why a store call failed, in words for the user (no network, a refusal, a timeout, a file too large).</summary>
public sealed class DinoStoreException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>What the store dialog needs from the store; the real one talks to dinoreader.sirous.uk, a test stands in with fixtures.</summary>
public interface IDinoStoreSource
{
    Task<DinoStoreCatalog> LoadCatalogAsync(CancellationToken cancel);

    /// <summary>The path of a cover picture on disk (fetched once, then reused), or null when it cannot be had.</summary>
    Task<string?> FetchCoverAsync(DinoStoreEntry entry, CancellationToken cancel);

    /// <summary>Drops the saved cover of a book (one that would not decode), so it is fetched again next time.</summary>
    void DiscardCover(DinoStoreEntry entry);

    /// <summary>The path of the whole <c>.dino</c> file on disk. Nothing partial is ever left behind.</summary>
    Task<string> DownloadAsync(DinoStoreEntry entry, IProgress<DownloadProgress> progress, CancellationToken cancel);
}

/// <summary>
/// Talks to Dino Reader's story store (Slice 18). Plain GETs only: no cookies, no identifiers, no body. Only https URLs on the store's own
/// host under <c>/api/stories/</c> are fetched, and a redirect is never followed, so the app only ever talks to that host. A download is
/// written to <c>store\&lt;id&gt;-v&lt;version&gt;.dino.part</c> and renamed when whole; a finished file of the same version is reused.
/// </summary>
public sealed class DinoStoreClient : IDinoStoreSource, IDisposable
{
    public const string StoreFolder = "store";
    public const long MaxDownloadBytes = DinoBookReader.MaxTotalBytes;
    public const long MaxCoverBytes = 5 * 1024 * 1024;

    private const string AppFolder = "PrintTheDamnBook";
    private static readonly TimeSpan HeaderTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StalePartAge = TimeSpan.FromHours(1);

    private const string Unreachable = "Could not reach Dino's store.";
    private const string Refused = "Dino's store did not give that book.";
    private const string Redirected = "Dino's store sent me somewhere else, so I stopped.";

    private readonly HttpClient _http;
    private readonly string _root;

    /// <summary>A client that keeps its files under <paramref name="root"/> (the app's local folder by default); <paramref name="handler"/> is for tests.</summary>
    public DinoStoreClient(string? root = null, HttpMessageHandler? handler = null)
    {
        _root = root ?? DefaultRoot;
        _http = new HttpClient(handler ?? NewHandler(), disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolder, StoreFolder);

    public void Dispose()
    {
        _http.Dispose();
    }

    public async Task<DinoStoreCatalog> LoadCatalogAsync(CancellationToken cancel)
    {
        var url = new Uri(DinoStoreCatalog.BaseAddress + DinoStoreCatalog.ListPath);
        try
        {
            using var response = await SendAsync(url, cancel);
            var bytes = await ReadAllAsync(response, DinoStoreCatalog.MaxCatalogBytes, cancel);
            return DinoStoreCatalog.Parse(Encoding.UTF8.GetString(bytes));
        }
        catch (Exception ex) when (IsStoreFailure(ex, cancel))
        {
            throw Translate(ex);
        }
    }

    public async Task<string?> FetchCoverAsync(DinoStoreEntry entry, CancellationToken cancel)
    {
        if (entry.CoverUrl is null)
        {
            return null;
        }

        var path = DinoStoreFiles.CoverPath(_root, entry);
        if (File.Exists(path))
        {
            return path;
        }

        try
        {
            using var response = await SendAsync(entry.CoverUrl, cancel);
            var bytes = await ReadAllAsync(response, MaxCoverBytes, cancel);
            await DinoStoreFiles.WriteAtomicallyAsync(path, target => target.WriteAsync(bytes, cancel).AsTask());
            DeleteOtherCovers(path);
            return path;
        }
        catch (Exception ex) when (IsStoreFailure(ex, cancel))
        {
            DinoStoreLog.Write($"cover not saved ({ex.GetType().Name})");
            return null;
        }
    }

    /// <summary>Forgets a cached cover that could not be decoded, so the next list fetches it again.</summary>
    public void DiscardCover(DinoStoreEntry entry)
    {
        DinoStoreFiles.TryDelete(DinoStoreFiles.CoverPath(_root, entry));
    }

    private void DeleteOtherCovers(string keep)
    {
        try
        {
            var files = Directory.EnumerateFiles(Path.GetDirectoryName(keep)!).ToList();
            foreach (var file in DinoStoreFiles.OtherCoverVersions(files, keep))
            {
                DinoStoreFiles.TryDelete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Old covers are only wasted space.
        }
    }

    public async Task<string> DownloadAsync(DinoStoreEntry entry, IProgress<DownloadProgress> progress, CancellationToken cancel)
    {
        var final = DinoStoreFiles.FinalPath(_root, entry);
        if (File.Exists(final))
        {
            DinoStoreLog.Write($"download reused {Path.GetFileName(final)}");
            return final;
        }

        try
        {
            await DinoStoreFiles.WriteAtomicallyAsync(final, target => FetchToAsync(entry.DownloadUrl, target, progress, cancel));
            DinoStoreLog.Write($"download finished {Path.GetFileName(final)}");
            return final;
        }
        catch (Exception ex)
        {
            DinoStoreLog.Write($"download stopped ({(cancel.IsCancellationRequested ? "cancelled" : ex.GetType().Name)}), partial file removed");
            if (!IsStoreFailure(ex, cancel))
            {
                throw;
            }

            throw Translate(ex);
        }
    }

    /// <summary>
    /// Called once the dialog is closed and the downloaded file has been opened (or failed to open). A file the reader rejected as not a usable
    /// Dino book is deleted so the next pick downloads it again (a disk or lock error keeps it); once one opens, the older versions of that book are deleted.
    /// </summary>
    public void Settle(string path, DinoLoadResult result)
    {
        if (result == DinoLoadResult.Rejected)
        {
            DinoStoreFiles.TryDelete(path);
        }
        else if (result == DinoLoadResult.Opened)
        {
            DeleteOtherVersions(path);
        }
    }

    /// <summary>Removes <c>.part</c> files over an hour old that a crashed or killed run left in the store and cover folders.</summary>
    public void SweepStaleParts()
    {
        foreach (var folder in new[] { _root, Path.Combine(_root, DinoStoreFiles.CoverFolder) })
        {
            try
            {
                var files = Directory.Exists(folder) ? Directory.EnumerateFiles(folder).ToList() : [];
                foreach (var file in DinoStoreFiles.StalePartFiles(files).Where(IsOldPart))
                {
                    DinoStoreFiles.TryDelete(file);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Leftover files are only wasted space.
            }
        }
    }

    /// <summary>A part file untouched for an hour is a leftover; a younger one may belong to a download in another window.</summary>
    private static bool IsOldPart(string file)
    {
        return DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > StalePartAge;
    }

    private static SocketsHttpHandler NewHandler()
    {
        return new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false, ConnectTimeout = HeaderTimeout };
    }

    /// <summary>One plain GET; headers only, so the body streams. Refuses a URL off the allow-list, a redirect and any non-2xx answer.</summary>
    private async Task<HttpResponseMessage> SendAsync(Uri url, CancellationToken cancel)
    {
        if (!IsStoreUrl(url))
        {
            throw new DinoStoreException(Refused);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var headerLimit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        headerLimit.CancelAfter(HeaderTimeout);
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headerLimit.Token);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var redirected = (int)response.StatusCode is >= 300 and < 400;
        response.Dispose();
        throw new DinoStoreException(redirected ? Redirected : Refused);
    }

    private static bool IsStoreUrl(Uri url)
    {
        return DinoStoreCatalog.IsAllowedUrl(url) || string.Equals(url.GetLeftPart(UriPartial.Path), DinoStoreCatalog.BaseAddress + DinoStoreCatalog.ListPath, StringComparison.OrdinalIgnoreCase);
    }

    private async Task FetchToAsync(Uri url, Stream target, IProgress<DownloadProgress> progress, CancellationToken cancel)
    {
        using var response = await SendAsync(url, cancel);
        var total = response.Content.Headers.ContentLength;
        DinoStreamCopier.CheckLength(total, MaxDownloadBytes);
        await using var source = await response.Content.ReadAsStreamAsync(cancel);
        await DinoStreamCopier.CopyAsync(source, target, new CopyLimits(MaxDownloadBytes, total, StallTimeout), progress, cancel);
    }

    private static async Task<byte[]> ReadAllAsync(HttpResponseMessage response, long cap, CancellationToken cancel)
    {
        var total = response.Content.Headers.ContentLength;
        DinoStreamCopier.CheckLength(total, cap);
        await using var source = await response.Content.ReadAsStreamAsync(cancel);
        using var memory = new MemoryStream();
        await DinoStreamCopier.CopyAsync(source, memory, new CopyLimits(cap, total, StallTimeout), new Progress<DownloadProgress>(), cancel);
        return memory.ToArray();
    }

    /// <summary>A problem we can word for the user: network, timeout, refusal or this PC's disk (not the caller's own cancel).</summary>
    private static bool IsStoreFailure(Exception ex, CancellationToken cancel)
    {
        return ex is HttpRequestException or IOException or UnauthorizedAccessException or DinoStoreException or DinoBookException or DinoDiskException
            || ex is OperationCanceledException && !cancel.IsCancellationRequested;
    }

    private static DinoStoreException Translate(Exception ex)
    {
        return ex switch
        {
            DinoStoreException known => known,
            DinoBookException book => new DinoStoreException(book.Message, book),
            DinoDiskException or UnauthorizedAccessException => new DinoStoreException(DinoStreamCopier.DiskMessage, ex),
            OperationCanceledException => new DinoStoreException(DinoStreamCopier.TimedOut, ex),
            _ => new DinoStoreException(Unreachable, ex),
        };
    }

    /// <summary>Removes older downloads of the same book once the newest one has opened.</summary>
    private void DeleteOtherVersions(string keep)
    {
        try
        {
            var files = Directory.EnumerateFiles(_root, "*" + DinoStoreFiles.DinoExtension).ToList();
            foreach (var file in DinoStoreFiles.OtherVersions(files, keep))
            {
                DinoStoreFiles.TryDelete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Old versions are only wasted space.
        }
    }
}

/// <summary>Debug-only log lines for the store, so a scripted check can tell which path ran (reuse, finish, cancel).</summary>
public static class DinoStoreLog
{
    [System.Diagnostics.Conditional("DEBUG")]
    public static void Write(string message) => System.Diagnostics.Debug.WriteLine("[dino-store] " + message);
}
