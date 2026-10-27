using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.App.ViewModels;

/// <summary>A language in the Check step's language select; a right-to-left one is listed but cannot be chosen.</summary>
public sealed record DinoLanguageOption(string Code, string Label, bool IsEnabled);

/// <summary>Opening a Dino Reader book (Slice 15): the <c>.dino</c> file, its words seeded into the story editor, and the language select.</summary>
public sealed partial class MainViewModel
{
    private const string ReadingImagesText = "Reading images…";
    private const string ReadingBookText = "Reading the book…";
    private const string DinoFailurePrefix = "Could not open this Dino book: ";
    private const string FolderFailurePrefix = "Could not read this folder: ";
    private const string SavedDinoText = "Saved with this Dino book";
    private const string OwnLanguageSuffix = " (the book's own)";
    private const string RightToLeftSuffix = " · right-to-left books are not supported yet";
    private const string DinoUnknownFailure = "Could not open this Dino book.";
    private const string NoPrintableLanguageText = "This book is written right to left, and I can't print those yet.";
    private const string RightToLeftNoticeFormat = "This book is written right to left, which I can't print yet. Showing it in {0}.";
    private const string SavedLanguageSuffix = " (as saved with this book)";
    private const string SavedLanguageNotice = "The language this book was saved in is no longer offered, so its words stay as they were.";
    private const string TranslateFailureText = "Could not change the language.";
    private const string SkippedOneText = "1 page had no picture and was left out.";
    private const string SkippedManyText = " pages had no picture and were left out.";
    private const string HeadingCheckDino = "Check the order and the words";
    private const string SubheadingCheckDino = "Every page came with its words. Click one to change them or move them to a page of their own.";
    private const string SubheadingOpenLoadedDino = "Happy with this book? Next you check the order and the words.";

    private readonly Func<string?> _pickDino;

    /// <summary>The Dino book on screen; null for a folder.</summary>
    private OpenedDino? _dino;

    /// <summary>The words each picture came with, in the book's own language, by <c>book.json</c> key (the cover's is the title).</summary>
    private Dictionary<string, string> _dinoSource = new(StringComparer.OrdinalIgnoreCase);

    private bool _syncingLanguage;
    private CancellationTokenSource? _loadCancel;
    private CancellationTokenSource? _translateCancel;
    private string? _languageNotice;

    /// <summary>True once a load has run past the slow-wait mark: the Open step then says what it waits for and offers Cancel.</summary>
    [ObservableProperty]
    public partial bool IsLoadingSlow { get; set; }

    /// <summary>True while the words are being put into another language (the select waits).</summary>
    [ObservableProperty]
    public partial bool IsTranslating { get; set; }

    /// <summary>True once that has run past the slow-wait mark: the Check step then says so and offers Cancel.</summary>
    [ObservableProperty]
    public partial bool IsTranslatingSlow { get; set; }

    /// <summary>True when the book on screen came from a <c>.dino</c> file.</summary>
    [ObservableProperty]
    public partial bool IsDinoBook { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<DinoLanguageOption> DinoLanguages { get; set; } = [];

    /// <summary>The code of the language the words are shown in.</summary>
    [ObservableProperty]
    public partial string SelectedDinoLanguage { get; set; } = string.Empty;

    /// <summary>What the Open step names the thing being read: the images of a folder or the Dino book.</summary>
    [ObservableProperty]
    public partial string LoadingText { get; set; } = ReadingImagesText;

    /// <summary>Where the book came from, as the Open step shows it: the folder, or the <c>.dino</c> file.</summary>
    [ObservableProperty]
    public partial string SourcePath { get; set; } = string.Empty;

    /// <summary>The name the book goes by: the folder's name, or the Dino book's title.</summary>
    private string BookName => _dino is { } dino
        ? dino.Book.Title.Length > 0 ? dino.Book.Title : Path.GetFileNameWithoutExtension(dino.SourcePath)
        : Path.GetFileName(Path.TrimEndingDirectorySeparator(FolderPath));

    private string SavedTextForBook => _dino is null ? SavedText : SavedDinoText;

    /// <summary>The language to save in <c>book.json</c>: null for the book's own, and for a folder.</summary>
    private string? SavedLanguage => _dino is { } dino && !dino.Book.IsOwn(SelectedDinoLanguage) ? SelectedDinoLanguage : null;

    [RelayCommand]
    private async Task OpenDinoAsync()
    {
        if (IsLoading)
        {
            return;
        }

        var path = _pickDino();
        if (path is not null)
        {
            await LoadDinoAsync(path);
        }
    }

    /// <summary>Opens a <c>.dino</c> file; if called again before it finishes, only the newest call updates the view.</summary>
    public async Task LoadDinoAsync(string path)
    {
        var (version, cancel, slow) = BeginLoad(ReadingBookText);
        try
        {
            // No token on Task.Run: a cancelled open waits for the extraction to notice and stop, so a re-open never races it on the folder.
            var loaded = await Task.Run(() => ReadDino(path, cancel.Token));
            if (IsCurrentLoad(version, cancel))
            {
                Show(loaded.Dino!.Folder, loaded);
            }
            else
            {
                DiscardLoad(loaded, cancel);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the user (or replaced by a newer load): the view stays as it was.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (version == _loadVersion)
            {
                ShowFailure(FailureMessage(ex));
            }
        }
        finally
        {
            EndLoad(version, cancel, slow);
        }
    }

    /// <summary>The reason shown for a book that could not be opened: the file's own message when it is meant for the user, else a plain sentence.</summary>
    private static string FailureMessage(Exception ex)
    {
        return ex is DinoBookException or IOException or UnauthorizedAccessException ? DinoFailurePrefix + ex.Message : DinoUnknownFailure;
    }

    /// <summary>Starts a load: the newest one wins, an older one is cancelled, and the spinner shows only if the wait passes the slow-wait mark.</summary>
    private (int Version, CancellationTokenSource Cancel, CancellationTokenSource Slow) BeginLoad(string text)
    {
        var version = ++_loadVersion;
        _loadCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        var slow = new CancellationTokenSource();
        _loadCancel = cancel;
        LoadingText = text;
        IsLoadingSlow = false;
        OpenFailure = null;
        IsLoading = true;
        _ = AfterSlowWaitAsync(slow.Token, () => IsLoadingSlow = true);
        return (version, cancel, slow);
    }

    /// <summary>True only while this load is still the newest and has not been cancelled; checked on the UI thread right before its result is shown.</summary>
    private bool IsCurrentLoad(int version, CancellationTokenSource cancel)
    {
        return version == _loadVersion && !cancel.IsCancellationRequested;
    }

    /// <summary>Drops a result nobody will see: its thumbnails, and the cache folder it created unless a newer open may be using it.</summary>
    private void DiscardLoad(LoadedFolder loaded, CancellationTokenSource cancel)
    {
        loaded.Thumbnails.Clear();
        ReleaseMemory(null);
        var newerOpen = _loadCancel is not null && !ReferenceEquals(_loadCancel, cancel);
        if (loaded.Dino is { } dino && !newerOpen && !string.Equals(dino.Folder, _dino?.Folder, StringComparison.OrdinalIgnoreCase))
        {
            DinoBookCache.Discard(dino);
        }
    }

    private void EndLoad(int version, CancellationTokenSource cancel, CancellationTokenSource slow)
    {
        slow.Cancel();
        slow.Dispose();
        if (ReferenceEquals(_loadCancel, cancel))
        {
            _loadCancel = null;
        }

        cancel.Dispose();
        if (version == _loadVersion)
        {
            IsLoading = false;
            IsLoadingSlow = false;
            LoadingText = ReadingImagesText;
        }
    }

    /// <summary>Runs <paramref name="show"/> once the wait passes <see cref="SlowWait"/>, unless <paramref name="stop"/> is cancelled first (nothing under 500 ms flashes).</summary>
    private static async Task AfterSlowWaitAsync(CancellationToken stop, Action show)
    {
        try
        {
            await Task.Delay(SlowWait, stop);
            if (!stop.IsCancellationRequested)
            {
                show();
            }
        }
        catch (OperationCanceledException)
        {
            // The work finished first.
        }
    }

    [RelayCommand]
    private void CancelLoad()
    {
        _loadCancel?.Cancel();
    }

    private static LoadedFolder ReadDino(string path, CancellationToken cancel)
    {
        var dino = DinoBookCache.Open(path, cancel);
        var start = dino.Book.StartLanguage() ?? throw new DinoBookException(NoPrintableLanguageText);
        var pages = PageOrdering.FromFolder(dino.Files);
        cancel.ThrowIfCancellationRequested();
        var thumbnails = new ThumbnailLoader();
        thumbnails.Preload(pages.Select(p => p.Path));
        return new LoadedFolder(pages, thumbnails, DinoText(dino, start)) { Dino = dino };
    }

    /// <summary>The words saved for this book on an earlier visit, or the ones that came in the file.</summary>
    private static BookTextLoad DinoText(OpenedDino dino, DinoLanguage start)
    {
        return File.Exists(BookTextStore.PathIn(dino.Folder)) ? BookTextStore.Read(dino.Folder) : new BookTextLoad(SeedText(dino, start), true);
    }

    /// <summary>The words as the book came, in its starting language (its own, unless that is right-to-left).</summary>
    private static BookText SeedText(OpenedDino dino, DinoLanguage start)
    {
        var entries = new Dictionary<string, PageTextEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, text) in SourceWords(dino))
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                var band = key == dino.Pictures.CoverFile ? TextBand.Top : TextBand.Bottom;
                entries[key] = new PageTextEntry(dino.Book.In(text, start.Code), TextPlacement.OnPicture, band);
            }
        }

        return new BookText { Pages = entries, Language = start.IsOwn ? null : start.Code };
    }

    /// <summary>The words each picture came with: the title on the cover, the page's words on each page.</summary>
    private static IEnumerable<KeyValuePair<string, string>> SourceWords(OpenedDino dino)
    {
        if (dino.Pictures.CoverFile is { } cover)
        {
            yield return new(cover, dino.Book.Title);
        }

        for (var i = 0; i < dino.Pictures.PageFiles.Count; i++)
        {
            yield return new(dino.Pictures.PageFiles[i], dino.Book.Pages[i].Text);
        }
    }

    /// <summary>Fills the language select for the book just shown (empty for a folder) and puts back the language it was last saved in.</summary>
    private void OpenDinoLanguages(BookTextLoad load)
    {
        _syncingLanguage = true;
        try
        {
            IsDinoBook = _dino is not null;
            _dinoSource = _dino is { } dino ? new Dictionary<string, string>(SourceWords(dino), StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase);
            _languageNotice = null;
            DinoLanguages = _dino is { } opened ? LanguageOptions(opened.Book, load.Book.Language) : [];
            SelectedDinoLanguage = _dino is { } shown ? SelectedLanguageOf(shown.Book, load.Book.Language) : string.Empty;
        }
        finally
        {
            _syncingLanguage = false;
        }
    }

    /// <summary>The select's entries. A saved language the file no longer offers (or offers only right-to-left) stays listed, so the words saved in it are not mislabelled.</summary>
    private List<DinoLanguageOption> LanguageOptions(DinoBook book, string? savedLanguage)
    {
        var options = book.Languages().Select(ToOption).ToList();
        var saved = DinoBook.Primary(savedLanguage);
        if (IsSavedLanguageUnusable(book, options, saved))
        {
            options.RemoveAll(o => o.Code == saved);
            options.Add(new DinoLanguageOption(saved, DinoBook.DescribeLanguage(saved).Name + SavedLanguageSuffix, true));
            _languageNotice = SavedLanguageNotice;
        }

        return options.OrderByDescending(o => o.IsEnabled).ToList();
    }

    private static bool IsSavedLanguageUnusable(DinoBook book, List<DinoLanguageOption> options, string saved)
    {
        return saved.Length > 0 && !book.IsOwn(saved) && !options.Any(o => o.Code == saved && o.IsEnabled);
    }

    /// <summary>The saved language, else the starting language; says so when the book's own language is right-to-left and another was chosen.</summary>
    private string SelectedLanguageOf(DinoBook book, string? savedLanguage)
    {
        var saved = DinoBook.Primary(savedLanguage);
        if (saved.Length > 0 && !book.IsOwn(saved))
        {
            return saved;
        }

        var start = book.StartLanguage()!;
        if (!start.IsOwn)
        {
            _languageNotice = string.Format(System.Globalization.CultureInfo.CurrentCulture, RightToLeftNoticeFormat, start.Name);
        }

        return start.Code;
    }

    private static DinoLanguageOption ToOption(DinoLanguage language)
    {
        var label = language.Name + (language.IsOwn ? OwnLanguageSuffix : language.IsRightToLeft ? RightToLeftSuffix : string.Empty);
        return new DinoLanguageOption(language.Code, label, !language.IsRightToLeft);
    }

    partial void OnSelectedDinoLanguageChanged(string oldValue, string newValue)
    {
        if (_syncingLanguage || _dino is null || string.IsNullOrEmpty(oldValue) || string.IsNullOrEmpty(newValue))
        {
            return;
        }

        if (DinoLanguages.Any(l => l.Code == newValue && !l.IsEnabled))
        {
            RevertLanguage(oldValue);
            return;
        }

        _ = ApplyLanguageAsync(oldValue, newValue);
    }

    /// <summary>The arrow keys can land on a disabled (right-to-left) language; put the select back on the one in use.</summary>
    private void RevertLanguage(string code)
    {
        _syncingLanguage = true;
        try
        {
            SelectedDinoLanguage = code;
        }
        finally
        {
            _syncingLanguage = false;
        }
    }

    /// <summary>
    /// Shows every page's words in <paramref name="to"/>. A page whose words the user changed keeps them, so switching language never
    /// throws their work away; only words still as the book had them in <paramref name="from"/> are replaced. The translating runs off the UI
    /// thread; past the slow-wait mark the Check step says so and offers Cancel, which leaves every page as it was.
    /// </summary>
    private async Task ApplyLanguageAsync(string from, string to)
    {
        var dino = _dino!;
        var work = TranslationWork();
        using var cancel = new CancellationTokenSource();
        using var slow = new CancellationTokenSource();
        _translateCancel = cancel;
        IsTranslating = true;
        _ = AfterSlowWaitAsync(slow.Token, () => IsTranslatingSlow = true);
        try
        {
            var done = await Task.Run(() => Translate(dino.Book, work, from, to, cancel.Token), cancel.Token);
            if (ReferenceEquals(dino, _dino))
            {
                ApplyTranslated(done);
            }
        }
        catch (OperationCanceledException)
        {
            RevertIfStillOpen(dino, from);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (RevertIfStillOpen(dino, from))
            {
                Status = TranslateFailureText;
            }
        }
        finally
        {
            slow.Cancel();
            if (ReferenceEquals(_translateCancel, cancel))
            {
                _translateCancel = null;
                IsTranslatingSlow = false;
                IsTranslating = false;
            }
        }
    }

    /// <summary>Puts the select back on <paramref name="code"/> unless the book has been replaced meanwhile (the new book's select is not ours to touch).</summary>
    private bool RevertIfStillOpen(OpenedDino dino, string code)
    {
        if (!ReferenceEquals(dino, _dino))
        {
            return false;
        }

        RevertLanguage(code);
        return true;
    }

    /// <summary>Stops a translation under way for a book that is going away.</summary>
    private void CancelTranslation()
    {
        _translateCancel?.Cancel();
        _translateCancel = null;
        IsTranslatingSlow = false;
        IsTranslating = false;
    }

    [RelayCommand]
    private void CancelTranslate()
    {
        _translateCancel?.Cancel();
    }

    private List<TranslationJob> TranslationWork()
    {
        var work = new List<TranslationJob>();
        foreach (var page in _pageList.Pages)
        {
            if (KeyOf(page) is { } key && _dinoSource.TryGetValue(key, out var source) && page.Story is not null)
            {
                work.Add(new TranslationJob(page, source));
            }
        }

        return work;
    }

    private static List<TranslationResult> Translate(DinoBook book, List<TranslationJob> work, string from, string to, CancellationToken cancel)
    {
        var done = new List<TranslationResult>(work.Count);
        foreach (var job in work)
        {
            cancel.ThrowIfCancellationRequested();
            done.Add(new TranslationResult(job.Page, book.In(job.Source, from), book.In(job.Source, to)));
        }

        return done;
    }

    private void ApplyTranslated(List<TranslationResult> done)
    {
        foreach (var result in done)
        {
            if (result.Page.Story is { } story && story.Text == result.FromText)
            {
                SetStory(result.Page, story with { Text = result.ToText });
            }
        }

        ScheduleBookTextSave();
        Refresh();
    }

    private sealed record TranslationJob(BookPage Page, string Source);

    private sealed record TranslationResult(BookPage Page, string FromText, string ToText);

    /// <summary>Tells the user when pages of the file were left out for want of a picture; null when none were.</summary>
    private string? SkippedNotice() => _dino is { Book.SkippedPages: > 0 and var skipped } ? (skipped == 1 ? SkippedOneText : skipped + SkippedManyText) : null;

    /// <summary>What to tell the user on opening a Dino book: pages left out, and a language that was chosen for them.</summary>
    private string? DinoNotice()
    {
        var notice = string.Join(' ', new[] { SkippedNotice(), _languageNotice }.OfType<string>());
        return notice.Length > 0 ? notice : null;
    }
}
