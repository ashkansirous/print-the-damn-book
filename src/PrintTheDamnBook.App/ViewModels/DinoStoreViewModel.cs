using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.App.ViewModels;

/// <summary>How opening a <c>.dino</c> file ended: shown, rejected as not a usable Dino book, failed for another reason (disk, lock), or neither (cancelled or replaced by a newer open).</summary>
public enum DinoLoadResult
{
    Opened,
    Rejected,
    Failed,
    Skipped,
}

/// <summary>What the store dialog shows.</summary>
public enum DinoStoreState
{
    Loading,
    List,
    Failed,
    Downloading,
    Empty,
}

/// <summary>One book on the store's list: its words, its cover once it has arrived, and whether it can be chosen.</summary>
public sealed partial class DinoStoreCardViewModel : ObservableObject
{
    private const string ChooseTip = "Download and open";
    private const string UnsupportedTipFormat = "{0} books are not supported yet";
    private const char FirstStrongIsolate = '⁨';
    private const char PopDirectionalIsolate = '⁩';

    public DinoStoreCardViewModel(DinoStoreEntry entry)
    {
        Entry = entry;
        var language = DinoBook.DescribeLanguage(entry.Language);
        LanguageName = language.Name;
        IsRightToLeft = language.IsRightToLeft;
        Meta = DinoStoreCatalog.MetaText(entry);
        IsEnabled = DinoStoreCatalog.IsSupported(entry);
        Tooltip = IsEnabled ? ChooseTip : string.Format(CultureInfo.CurrentCulture, UnsupportedTipFormat, EnglishName(entry.Language));
    }

    public DinoStoreEntry Entry { get; }

    public string Title => Entry.Title;

    public string LanguageName { get; }

    public string Meta { get; }

    public bool IsEnabled { get; }

    public string Tooltip { get; }

    /// <summary>Own reading direction of the title (a Farsi title runs right to left inside a left-to-right window).</summary>
    public bool IsRightToLeft { get; }

    /// <summary>The line under the language: "12 pages · ages 4–7".</summary>
    public string LanguageAndMeta => Meta.Length > 0 ? Isolated(LanguageName) + " · " + Meta : Isolated(LanguageName);

    /// <summary>Keeps a right-to-left language name from reordering the text around it (first-strong isolate).</summary>
    private static string Isolated(string text)
    {
        return FirstStrongIsolate + text + PopDirectionalIsolate;
    }

    /// <summary>The cover, once loaded; null until then (the card shows a placeholder).</summary>
    [ObservableProperty]
    public partial ImageSource? Cover { get; set; }

    private static string EnglishName(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(DinoBook.Primary(code)).EnglishName;
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }
}

/// <summary>A choice in the dialog's language select: "All languages · 10" (empty code) or "English · 4".</summary>
public sealed record DinoStoreLanguageOption(string Code, string Label);

/// <summary>
/// The "Stories from Dino" dialog (Slice 18): loads the list, filters it by language, downloads the chosen book and hands back the
/// <c>.dino</c> path. Each wait is a state of its own; Cancel stops the work and, for a download, returns to the list.
/// </summary>
public sealed partial class DinoStoreViewModel : ObservableObject
{
    private const string AllLanguagesFormat = "All languages · {0}";
    private const string LanguageFormat = "{0} · {1}";
    private const string ProgressFormat = "{0} of {1} MB";
    private const string ProgressUnknownFormat = "{0} MB";
    private const string ConnectionHeading = "I couldn't reach Dino's store";
    private const string ConnectionHint = "Check the internet connection and try again. Folders and .dino files on this PC still open as usual.";
    private const string OtherHeading = "That didn't work";
    private const string OtherHint = "Try again, or go back to the list and pick another book.";
    private const string OtherHintNoList = "Try again in a moment.";
    private const string UnexpectedFailure = "Something went wrong with Dino's store.";
    private const string CancelledNote = "Download cancelled.";
    private const double BytesPerMb = 1024 * 1024;
    private const int CoverWidth = 240;
    private const int NoDownloadPercent = 0;
    private const int CoverParallelism = 4;
    private const char LeftToRightMark = '‎';

    private readonly IDinoStoreSource _source;
    private readonly List<CancellationTokenSource> _sources = [];
    private readonly List<Task> _running = [];
    private readonly Queue<DinoStoreCardViewModel> _coverQueue = new();
    private readonly HashSet<DinoStoreCardViewModel> _coverTaken = [];
    private readonly HashSet<DinoStoreCardViewModel> _coverRetried = [];
    private List<DinoStoreCardViewModel> _all = [];
    private Dictionary<DinoStoreEntry, DinoStoreCardViewModel> _cardOf = [];
    private DinoStoreCatalog? _catalog;
    private CancellationTokenSource? _work;
    private CancellationTokenSource? _covers;
    private DinoStoreCardViewModel? _retryCard;
    private int _coverWorkers;
    private bool _syncingLanguage;

    public DinoStoreViewModel(IDinoStoreSource source)
    {
        _source = source;
    }

    /// <summary>Raised when the dialog should close: with the downloaded file's path, or null for Close/Cancel.</summary>
    public event Action<string?>? CloseRequested;

    [ObservableProperty]
    public partial DinoStoreState State { get; set; } = DinoStoreState.Loading;

    public bool IsLoading => State == DinoStoreState.Loading;

    public bool IsList => State == DinoStoreState.List;

    public bool IsFailed => State == DinoStoreState.Failed;

    public bool IsDownloading => State == DinoStoreState.Downloading;

    public bool IsEmpty => State == DinoStoreState.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<DinoStoreCardViewModel> Cards { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<DinoStoreLanguageOption> Languages { get; set; } = [];

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = string.Empty;

    /// <summary>The line above the grid after a cancelled download; empty otherwise.</summary>
    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DinoStoreCardViewModel? Downloading { get; set; }

    [ObservableProperty]
    public partial string DownloadText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DownloadTitle { get; set; } = string.Empty;

    /// <summary>0 to 100.</summary>
    [ObservableProperty]
    public partial double DownloadPercent { get; set; }

    /// <summary>Why the list could not be had, for the failed state's detail (the heading is fixed copy).</summary>
    [ObservableProperty]
    public partial string FailureReason { get; set; } = string.Empty;

    /// <summary>The failed state's heading: the connection one only when the failure was about reaching the store.</summary>
    [ObservableProperty]
    public partial string FailureHeading { get; set; } = ConnectionHeading;

    /// <summary>What to do next, matching <see cref="FailureHeading"/>.</summary>
    [ObservableProperty]
    public partial string FailureHint { get; set; } = ConnectionHint;

    /// <summary>True when a download failed and the list is still there to go back to (a failed list has none).</summary>
    [ObservableProperty]
    public partial bool CanGoBack { get; set; }

    partial void OnStateChanged(DinoStoreState value)
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsList));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (!_syncingLanguage && _catalog is not null)
        {
            ShowCards();
        }
    }

    /// <summary>Asks the store for its list; the dialog calls this when it opens and again for Try again.</summary>
    [RelayCommand]
    public Task LoadAsync()
    {
        var cancel = BeginWork();
        ResetCovers();
        _retryCard = null;
        ResetFailure();
        State = DinoStoreState.Loading;
        Note = string.Empty;
        return Track(LoadListAsync(cancel));
    }

    private async Task LoadListAsync(CancellationTokenSource work)
    {
        try
        {
            var catalog = await _source.LoadCatalogAsync(work.Token);
            if (!work.IsCancellationRequested)
            {
                ShowCatalog(catalog);
            }
        }
        catch (OperationCanceledException)
        {
            // Closed or replaced: nothing to show.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (IsCurrent(work))
            {
                Fail(ex, canGoBack: false);
            }

            DinoStoreLog.Write("list failed: " + ex.Message);
        }
    }

    /// <summary>The words for the user: a store or book message as it is, anything unexpected as a plain sentence.</summary>
    private static string ReasonOf(Exception ex)
    {
        return ex is DinoStoreException or DinoBookException or DinoDiskException ? ex.Message : UnexpectedFailure;
    }

    /// <summary>True while <paramref name="work"/> is still the newest list or download (a Try again replaces it).</summary>
    private bool IsCurrent(CancellationTokenSource work)
    {
        return ReferenceEquals(_work, work);
    }

    /// <summary>Downloads the chosen book, then closes the dialog with its path.</summary>
    [RelayCommand]
    public Task ChooseAsync(DinoStoreCardViewModel? card)
    {
        if (card is not { IsEnabled: true } || State != DinoStoreState.List)
        {
            return Task.CompletedTask;
        }

        return Track(DownloadBookAsync(card));
    }

    private async Task DownloadBookAsync(DinoStoreCardViewModel card)
    {
        var cancel = BeginWork();
        _retryCard = card;
        StartDownload(card);
        try
        {
            var progress = new Progress<DownloadProgress>(p => ReportProgress(cancel, p));
            var path = await _source.DownloadAsync(card.Entry, progress, cancel.Token);
            FinishDownload(cancel, path);
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(cancel))
            {
                BackToList(CancelledNote);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A Cancel that raced an abort can come up as a network or IO error; it is still a cancel.
            if (IsCurrent(cancel) && cancel.IsCancellationRequested)
            {
                DinoStoreLog.Write("download error after cancel treated as cancelled: " + ex.GetType().Name);
                BackToList(CancelledNote);
            }
            else if (IsCurrent(cancel))
            {
                Fail(ex, canGoBack: true);
            }

            DinoStoreLog.Write("download failed: " + ex.Message);
        }
    }

    /// <summary>A download that finished after Cancel or Close is not opened (the finished file stays in the store and is reused next time); otherwise the dialog closes with its path.</summary>
    private void FinishDownload(CancellationTokenSource work, string path)
    {
        if (!work.IsCancellationRequested)
        {
            if (IsCurrent(work))
            {
                CloseRequested?.Invoke(path);
            }

            return;
        }

        if (IsCurrent(work))
        {
            BackToList(CancelledNote);
        }
    }

    /// <summary>Try again: downloads the book that failed again, or asks for the list again when the list is what failed.</summary>
    [RelayCommand]
    public Task RetryAsync()
    {
        return _retryCard is { } card ? Track(DownloadBookAsync(card)) : LoadAsync();
    }

    /// <summary>Cancel on a download returns to the list with a note; Cancel while loading closes the dialog.</summary>
    [RelayCommand]
    public void Cancel()
    {
        var wasDownloading = State == DinoStoreState.Downloading;
        _work?.Cancel();
        if (!wasDownloading)
        {
            CancelAll();
            CloseRequested?.Invoke(null);
        }
    }

    /// <summary>Close button and Escape: stops whatever runs and closes.</summary>
    [RelayCommand]
    public void Close()
    {
        CancelAll();
        CloseRequested?.Invoke(null);
    }

    /// <summary>Stops everything and waits until nothing of this dialog is still running, so the client can then be disposed.</summary>
    public async Task StopAsync()
    {
        CancelAll();
        await Task.WhenAll(_running.ToArray()).ContinueWith(_ => { }, TaskScheduler.Default);
        foreach (var source in _sources)
        {
            source.Dispose();
        }

        _sources.Clear();
    }

    private void CancelAll()
    {
        _work?.Cancel();
        _covers?.Cancel();
    }

    /// <summary>Puts the failed state's texts back to their defaults, so none of them is left over from an earlier failure.</summary>
    private void ResetFailure()
    {
        FailureReason = string.Empty;
        FailureHeading = ConnectionHeading;
        FailureHint = ConnectionHint;
        CanGoBack = false;
    }

    private void Fail(Exception ex, bool canGoBack)
    {
        if (!canGoBack)
        {
            _retryCard = null;
        }

        FailureReason = ReasonOf(ex);
        var aboutConnection = IsAboutConnection(ex);
        FailureHeading = aboutConnection ? ConnectionHeading : OtherHeading;
        CanGoBack = canGoBack && _catalog is not null;
        FailureHint = aboutConnection ? ConnectionHint : CanGoBack ? OtherHint : OtherHintNoList;
        State = DinoStoreState.Failed;
    }

    /// <summary>
    /// True only for a failure of the network itself: an <see cref="HttpRequestException"/> with no status, a <see cref="DinoNetworkException"/> (the response stream
    /// dropped), a timeout (an <see cref="OperationCanceledException"/>; Fail is only reached when the user did not cancel) or a stalled or cut-short transfer. Everything
    /// else, including a bare <see cref="IOException"/> (never guessed at: the disk raises <see cref="DinoDiskException"/> and the network its own type where each is touched),
    /// a store refusal (404, 403, a redirect) and a damaged book, is not, where "check your connection" would mislead.
    /// </summary>
    private static bool IsAboutConnection(Exception ex)
    {
        return ex switch
        {
            DinoBookException book => book.Message is DinoStreamCopier.TimedOut or DinoStreamCopier.CutShort,
            DinoStoreException store => store.InnerException is not null && IsAboutConnection(store.InnerException),
            DinoNetworkException => true,
            HttpRequestException http => http.StatusCode is null,
            OperationCanceledException => true,
            _ => false,
        };
    }

    /// <summary>Back to list on a failed download: the list is shown again, with no note.</summary>
    [RelayCommand]
    public void BackToLastList()
    {
        _retryCard = null;
        BackToList(string.Empty);
    }

    private Task Track(Task task)
    {
        _running.RemoveAll(t => t.IsCompleted);
        _running.Add(task);
        return task;
    }

    /// <summary>The list and a download share one token (a new one replaces the old); covers have their own, so choosing a book never stops them.</summary>
    private CancellationTokenSource BeginWork()
    {
        _work?.Cancel();
        var cancel = new CancellationTokenSource();
        _sources.Add(cancel);
        _work = cancel;
        return cancel;
    }

    private void ShowCatalog(DinoStoreCatalog catalog)
    {
        _catalog = catalog;
        _all = catalog.Entries.Select(e => new DinoStoreCardViewModel(e)).ToList();
        IEqualityComparer<DinoStoreEntry> byReference = ReferenceEqualityComparer.Instance;
        _cardOf = _all.ToDictionary(c => c.Entry, byReference);
        _syncingLanguage = true;
        try
        {
            Languages = LanguageOptions(catalog);
            SelectedLanguage = string.Empty;
        }
        finally
        {
            _syncingLanguage = false;
        }

        ShowCards();
        State = _all.Count == 0 ? DinoStoreState.Empty : DinoStoreState.List;
    }

    private static List<DinoStoreLanguageOption> LanguageOptions(DinoStoreCatalog catalog)
    {
        var options = new List<DinoStoreLanguageOption> { new(string.Empty, string.Format(CultureInfo.CurrentCulture, AllLanguagesFormat, catalog.Entries.Count)) };
        options.AddRange(catalog.Languages().Select(l => new DinoStoreLanguageOption(l.Code, string.Format(CultureInfo.CurrentCulture, LanguageFormat, l.Name + LeftToRightMark, l.Count))));
        return options;
    }

    private void ShowCards()
    {
        var shown = _catalog!.InLanguage(SelectedLanguage).Select(e => _cardOf[e]).ToList();
        Cards = shown;
        QueueVisibleCovers();
    }

    /// <summary>A fresh cover run for a new list: the old run is stopped and nothing is remembered from it.</summary>
    private void ResetCovers()
    {
        _covers?.Cancel();
        _covers = new CancellationTokenSource();
        _sources.Add(_covers);
        _coverQueue.Clear();
        _coverTaken.Clear();
        _coverRetried.Clear();
        _coverWorkers = 0;
    }

    /// <summary>Queues covers for the cards shown under the current filter that have not been fetched yet, and starts a few workers.</summary>
    private void QueueVisibleCovers()
    {
        _coverQueue.Clear();
        foreach (var card in Cards.Where(c => !_coverTaken.Contains(c)))
        {
            _coverQueue.Enqueue(card);
        }

        while (_coverWorkers < CoverParallelism && _coverQueue.Count > 0 && _covers is { } covers)
        {
            _coverWorkers++;
            Track(CoverWorkerAsync(covers));
        }
    }

    /// <summary>Takes cards off the queue one at a time (a few workers run side by side, all on the UI thread); a cover that fails stays a placeholder.</summary>
    private async Task CoverWorkerAsync(CancellationTokenSource run)
    {
        try
        {
            while (_coverQueue.Count > 0)
            {
                run.Token.ThrowIfCancellationRequested();
                var card = _coverQueue.Dequeue();
                _coverTaken.Add(card);
                if (!await LoadCoverAsync(card, run.Token) && card.Entry.CoverUrl is not null && _coverRetried.Add(card))
                {
                    // One more go at the end of the queue, so a hiccup does not leave a placeholder until Try again.
                    DinoStoreLog.Write("cover queued for one retry");
                    _coverQueue.Enqueue(card);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Closed or replaced by a new list.
        }
        finally
        {
            if (ReferenceEquals(run, _covers))
            {
                _coverWorkers--;
            }
        }
    }

    /// <summary>True when the card now has its cover; a failure leaves the placeholder (the caller may try once more).</summary>
    private async Task<bool> LoadCoverAsync(DinoStoreCardViewModel card, CancellationToken cancel)
    {
        try
        {
            var file = await _source.FetchCoverAsync(card.Entry, cancel);
            if (file is not null)
            {
                card.Cover = await Task.Run(() => ImageDecoder.Decode(file, CoverWidth), cancel);
                if (card.Cover is null)
                {
                    _source.DiscardCover(card.Entry);
                }
            }

            return card.Cover is not null;
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            // One bad cover stays a placeholder; the other covers and the dialog carry on.
            DinoStoreLog.Write("cover failed: " + ex.GetType().Name);
            return false;
        }
    }

    private void StartDownload(DinoStoreCardViewModel card)
    {
        ResetFailure();
        Downloading = card;
        DownloadTitle = card.Title;
        DownloadPercent = NoDownloadPercent;
        DownloadText = string.Empty;
        Note = string.Empty;
        State = DinoStoreState.Downloading;
    }

    private void ReportProgress(CancellationTokenSource work, DownloadProgress progress)
    {
        if (!IsCurrent(work) || work.IsCancellationRequested)
        {
            return;
        }

        DownloadPercent = progress.Total is > 0 ? Math.Min(100.0, 100.0 * progress.Received / progress.Total.Value) : NoDownloadPercent;
        DownloadText = ProgressText(progress);
    }

    /// <summary>"3.2 of 8.4 MB", or just "3.2 MB" when the store gave no length.</summary>
    internal static string ProgressText(DownloadProgress progress)
    {
        var got = (progress.Received / BytesPerMb).ToString("0.0", CultureInfo.CurrentCulture);
        return progress.Total is > 0
            ? string.Format(CultureInfo.CurrentCulture, ProgressFormat, got, (progress.Total.Value / BytesPerMb).ToString("0.0", CultureInfo.CurrentCulture))
            : string.Format(CultureInfo.CurrentCulture, ProgressUnknownFormat, got);
    }

    private void BackToList(string note)
    {
        Downloading = null;
        Note = note;
        ResetFailure();
        State = DinoStoreState.List;
    }
}
