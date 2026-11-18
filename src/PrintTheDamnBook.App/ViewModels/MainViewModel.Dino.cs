using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.App.ViewModels;

/// <summary>A language in the Check step's language select; one the app cannot set in type yet (Hebrew ...) is listed but cannot be chosen.</summary>
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
    private const string UnsupportedSuffix = " · this language is not supported yet";
    private const string DinoUnknownFailure = "Could not open this Dino book.";
    private const string NoPrintableLanguageText = "This book is in a language I can't print yet.";
    private const string SavedWordsLostText = "Another window is using this book's folder, so your saved words could not be carried over.";
    private const string UnsupportedNoticeFormat = "This book's own language is not supported yet. Showing it in {0}.";
    private const string SavedLanguageSuffix = " (as saved with this book)";
    private const string SavedLanguageNotice = "The language this book was saved in is no longer offered, so its words stay as they were.";
    private const string KeptPageOne = "Page ";
    private const string KeptPageMany = "Pages ";
    private const string KeptTextTail = " kept your own text.";
    private const string TranslateFailureText = "Could not change the language.";
    private const string SkippedOneText = "1 page had no picture and was left out.";
    private const string SkippedManyText = " pages had no picture and were left out.";
    private const string HeadingCheckDino = "Check the order and the words";
    private const string SubheadingCheckDino = "Every page came with its words. Click one to change them or move them to a page of their own.";
    private const string HandoverLoadingTitle = "Opening a book from Dino Reader…";
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

    /// <summary>Which pages kept the user's own words at the last language switch; shown on the Check step beside the language select, cleared when a book opens.</summary>
    [ObservableProperty]
    public partial string? KeptTextNotice { get; set; }

    /// <summary>The alert of an unusable hand-over that the start of a load dismissed; put back if that load is cancelled.</summary>
    private string? _stashedHandoverProblem;

    /// <summary>The hand-over alert a cancelled load set aside (see <see cref="RestoreHandoverProblem"/>); null when none waits.</summary>
    internal string? StashedHandoverProblem => _stashedHandoverProblem;

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

    /// <summary>The file name of a hand-over that could not be opened (Slice 19); the Open step shows a dismissible alert while it is set.</summary>
    [ObservableProperty]
    public partial string? HandoverProblemFile { get; set; }

    /// <summary>The file name of the hand-over being read; the Open step says Dino Reader handed it over while it is set.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHandoverText))]
    public partial string? HandoverFile { get; set; }

    /// <summary>" in Farsi" when the hand-over asked for a language, else empty.</summary>
    [ObservableProperty]
    public partial string HandoverLanguageSuffix { get; set; } = string.Empty;

    /// <summary>True once a hand-over has run past the slow-wait mark: the drop card then tells the user what is happening.</summary>
    public bool ShowHandoverText => HandoverFile is not null && IsLoadingSlow;

    partial void OnIsLoadingSlowChanged(bool value) => OnPropertyChanged(nameof(ShowHandoverText));

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

    [RelayCommand]
    private void DismissHandoverProblem()
    {
        HandoverProblemFile = null;
    }

    /// <summary>Shows the hand-over alert for <paramref name="fileName"/> on the Open step.</summary>
    public void ReportHandoverProblem(string fileName)
    {
        ClearHandoverText();
        HandoverProblemFile = fileName;
    }

    /// <summary>Opens the book another app handed over: a bad path shows the alert on the Open step, a good one lands on the Check step in the language asked for.</summary>
    public async Task LoadHandoverAsync(HandoverRequest request)
    {
        if (!request.IsUsable)
        {
            System.Diagnostics.Debug.WriteLine($"[handover] rejected file={request.FileName}");
            HandoverProblemFile = request.FileName;
            return;
        }

        await LoadDinoAsync(request.Path, request);
    }

    /// <summary>
    /// Opens a <c>.dino</c> file; if called again before it finishes, only the newest call updates the view. <see cref="DinoLoadResult.Opened"/>
    /// only when this call's book is now the open one; <see cref="DinoLoadResult.Rejected"/> only when the reader refused the file as not a usable Dino book.
    /// </summary>
    public Task<DinoLoadResult> LoadDinoAsync(string path) => LoadDinoAsync(path, null);

    private async Task<DinoLoadResult> LoadDinoAsync(string path, HandoverRequest? handover)
    {
        var (version, cancel, slow) = BeginLoad(handover is null ? ReadingBookText : HandoverLoadingTitle);
        var result = DinoLoadResult.Skipped;
        try
        {
            AnnounceHandover(handover);
            // No token on Task.Run: a cancelled open waits for the extraction to notice and stop, so a re-open never races it on the folder.
            var loaded = await Task.Run(() => ReadDino(path, cancel.Token));
            if (IsCurrentLoad(version, cancel))
            {
                Show(loaded.Dino!.Folder, loaded);
                result = DinoLoadResult.Opened;
                await ApplyHandoverLanguageSafelyAsync(handover?.Language);
            }
            else
            {
                DiscardLoad(loaded);
                RestoreHandoverProblem(version);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the user (or replaced by a newer load): the view stays as it was.
            RestoreHandoverProblem(version);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (version == _loadVersion)
            {
                _stashedHandoverProblem = null;
                ShowDinoFailure(ex, handover);
                result = ex is DinoBookException ? DinoLoadResult.Rejected : DinoLoadResult.Failed;
            }
        }
        finally
        {
            EndLoad(version, cancel, slow);
            if (version == _loadVersion)
            {
                ClearHandoverText();
            }
        }

        return result;
    }

    /// <summary>Shows a failure from outside the open itself (the store dialog) in the Open step's normal failure status.</summary>
    public void ReportOpenFailure(Exception ex)
    {
        ShowFailure(FailureMessage(ex));
    }

    /// <summary>A load the user cancelled brings back the alert for the unusable hand-over that starting it had dismissed, so the failure is still explained.</summary>
    private void RestoreHandoverProblem(int version)
    {
        if (version == _loadVersion)
        {
            HandoverProblemFile ??= _stashedHandoverProblem;
            _stashedHandoverProblem = null;
        }
    }

    /// <summary>Sets the drop card's "Dino Reader handed me ..." text for this load; a load that is not a hand-over clears it.</summary>
    private void AnnounceHandover(HandoverRequest? handover)
    {
        HandoverFile = handover?.FileName;
        HandoverLanguageSuffix = handover?.Language is { } code ? " in " + DinoBook.DescribeLanguage(code).Name : string.Empty;
    }

    private void ClearHandoverText()
    {
        HandoverFile = null;
        HandoverLanguageSuffix = string.Empty;
    }

    /// <summary>A hand-over that cannot be read shows the alert; a book picked by hand keeps its normal message.</summary>
    private void ShowDinoFailure(Exception ex, HandoverRequest? handover)
    {
        if (handover is null || ex is DinoBookException or NoPrintableLanguageException)
        {
            ShowFailure(FailureMessage(ex));
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[handover] could not be read file={handover.FileName}: {ex.GetType().Name}");
        HandoverProblemFile = handover.FileName;
    }

    /// <summary>The book is already on screen, so a failure to switch its language is reported as that, never as the book failing to open.</summary>
    private async Task ApplyHandoverLanguageSafelyAsync(string? requested)
    {
        try
        {
            await ApplyHandoverLanguageAsync(requested);
        }
        catch (OperationCanceledException)
        {
            // The user changed the language themselves first; their choice stands.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine($"[handover] language switch failed: {ex.GetType().Name}");
            Status = TranslateFailureText;
        }
    }

    /// <summary>Switches the freshly shown hand-over to the language it asked for when the book offers it, keeping the loading state until the words are switched; otherwise the book's own stays.</summary>
    private async Task ApplyHandoverLanguageAsync(string? requested)
    {
        if (requested is null || _dino is null)
        {
            return;
        }

        var code = DinoBook.Primary(requested);
        var offered = DinoLanguages.Any(l => l.Code == code && l.IsEnabled);
        System.Diagnostics.Debug.WriteLine($"[handover] language requested={code} offered={offered} selected={SelectedDinoLanguage}");
        if (!offered || code == SelectedDinoLanguage)
        {
            return;
        }

        var from = SelectedDinoLanguage;
        _syncingLanguage = true;
        try
        {
            SelectedDinoLanguage = code;
        }
        finally
        {
            _syncingLanguage = false;
        }

        await ApplyLanguageAsync(from, code, reconcileOnly: false);
    }

    /// <summary>The reason shown for a book that could not be opened: the file's own message when it is meant for the user, else a plain sentence.</summary>
    private static string FailureMessage(Exception ex)
    {
        var explained = ex is DinoBookException or NoPrintableLanguageException or IOException or UnauthorizedAccessException;
        return explained ? DinoFailurePrefix + ex.Message : DinoUnknownFailure;
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
        _stashedHandoverProblem = HandoverProblemFile ?? _stashedHandoverProblem;
        HandoverProblemFile = null;
        ClearHandoverText();
        IsLoading = true;
        _ = AfterSlowWaitAsync(slow.Token, () => IsLoadingSlow = true);
        return (version, cancel, slow);
    }

    /// <summary>True only while this load is still the newest and has not been cancelled; checked on the UI thread right before its result is shown.</summary>
    private bool IsCurrentLoad(int version, CancellationTokenSource cancel)
    {
        return version == _loadVersion && !cancel.IsCancellationRequested;
    }

    /// <summary>Drops a result nobody will see: its thumbnails, and (off the UI thread, since it takes the cross-process mutex) its hold on the cache folder, which is removed when this open created it and nothing else uses it.</summary>
    private static void DiscardLoad(LoadedFolder loaded)
    {
        loaded.Thumbnails.Clear();
        ReleaseMemory(null);
        if (loaded.Dino is { } dino)
        {
            _ = Task.Run(() => DinoBookCache.Discard(dino));
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
        return LoadOpened(DinoBookCache.Open(path, cancel), cancel);
    }

    /// <summary>Shows a book that is already extracted, without the shared cache (the test seam of <see cref="ReadDino"/>'s second half).</summary>
    internal void ShowOpened(OpenedDino dino)
    {
        Show(dino.Folder, LoadOpened(dino, CancellationToken.None));
    }

    /// <summary>The pictures' pages in reading order, with their words: what a language switch works on (read by tests).</summary>
    internal IReadOnlyList<BookPage> BookPages => _pageList.Pages;

    private static LoadedFolder LoadOpened(OpenedDino dino, CancellationToken cancel)
    {
        try
        {
            var start = dino.Book.StartLanguage() ?? throw new NoPrintableLanguageException(NoPrintableLanguageText);
            var pages = PageOrdering.FromFolder(dino.Files);
            cancel.ThrowIfCancellationRequested();
            var thumbnails = new ThumbnailLoader();
            thumbnails.Preload(pages.Select(p => p.Path));
            return new LoadedFolder(pages, thumbnails, DinoText(dino, start)) { Dino = dino };
        }
        catch
        {
            DinoBookCache.Discard(dino);
            throw;
        }
    }

    /// <summary>The words saved for this book on an earlier visit, or the ones that came in the file.</summary>
    private static BookTextLoad DinoText(OpenedDino dino, DinoLanguage start)
    {
        return File.Exists(BookTextStore.PathIn(dino.Folder)) ? BookTextStore.Read(dino.Folder) : new BookTextLoad(SeedText(dino, start), true);
    }

    /// <summary>The words as the book came, in its starting language (its own, unless that is not supported yet).</summary>
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
            KeptTextNotice = null;
            DinoLanguages = _dino is { } opened ? LanguageOptions(opened.Book, load.Book.Language) : [];
            SelectedDinoLanguage = _dino is { } shown ? SelectedLanguageOf(shown.Book, load.Book.Language) : string.Empty;
            LanguageSwitch = ReconcileLanguageOnOpen();
        }
        finally
        {
            _syncingLanguage = false;
        }
    }

    /// <summary>The select's entries. A saved language the file no longer offers (or offers only an unsupported one) stays listed, so the words saved in it are not mislabelled.</summary>
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

    /// <summary>The saved language when the book has it, else the starting language; says so when the book's own language is not supported and another was chosen.</summary>
    private string SelectedLanguageOf(DinoBook book, string? savedLanguage)
    {
        var saved = DinoBook.Primary(savedLanguage);
        if (saved.Length > 0 && !book.IsOwn(saved) && book.Languages().Any(l => l.Code == saved))
        {
            return saved;
        }

        var start = book.StartLanguage()!;
        if (!start.IsOwn)
        {
            _languageNotice = string.Format(System.Globalization.CultureInfo.CurrentCulture, UnsupportedNoticeFormat, start.Name);
        }

        return start.Code;
    }

    private static DinoLanguageOption ToOption(DinoLanguage language)
    {
        var label = language.Name + (language.IsOwn ? OwnLanguageSuffix : language.IsSupported ? string.Empty : UnsupportedSuffix);
        return new DinoLanguageOption(language.Code, label, language.IsSupported);
    }

    /// <summary>The language switch started last (finished when it completes); awaited by tests, otherwise fire-and-forget.</summary>
    internal Task LanguageSwitch { get; private set; } = Task.CompletedTask;

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

        _direction.LanguageChanged();
        LanguageSwitch = ApplyLanguageAsync(oldValue, newValue, reconcileOnly: false);
    }

    /// <summary>The arrow keys can land on a disabled (unsupported) language; put the select back on the one in use.</summary>
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
    private async Task ApplyLanguageAsync(string from, string to, bool reconcileOnly)
    {
        var dino = _dino!;
        var work = TranslationWork();
        using var cancel = new CancellationTokenSource();
        using var slow = new CancellationTokenSource();
        _translateCancel?.Cancel();
        _translateCancel = cancel;
        IsTranslating = true;
        _ = AfterSlowWaitAsync(slow.Token, () => IsTranslatingSlow = true);
        try
        {
            var done = await Task.Run(() =>
            {
                TranslateProbe?.Invoke(cancel.Token);
                return Translate(dino.Book, work, to, cancel.Token);
            }, cancel.Token);
            if (IsCurrentRun(cancel))
            {
                ApplyRun(done, reconcileOnly);
            }
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentRun(cancel))
            {
                RevertLanguage(from);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (IsCurrentRun(cancel))
            {
                RevertLanguage(from);
                Status = reconcileOnly ? Status : TranslateFailureText;
            }
        }
        finally
        {
            slow.Cancel();
            if (IsCurrentRun(cancel))
            {
                _translateCancel = null;
                IsTranslatingSlow = false;
                IsTranslating = false;
            }
        }
    }

    /// <summary>Test seam: runs on the translating thread before the work starts, so a test can hold or fail a run.</summary>
    internal Action<CancellationToken>? TranslateProbe { get; set; }

    /// <summary>The current run is the one whose source is <c>_translateCancel</c>; a run that was superseded or whose book closed is not, and does nothing.</summary>
    private bool IsCurrentRun(CancellationTokenSource cancel)
    {
        return ReferenceEquals(_translateCancel, cancel);
    }

    private void ApplyRun(List<TranslationResult> done, bool reconcileOnly)
    {
        if (reconcileOnly)
        {
            ReconcileTranslated(done);
        }
        else
        {
            ApplyTranslated(done);
        }
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

    private static List<TranslationResult> Translate(DinoBook book, List<TranslationJob> work, string to, CancellationToken cancel)
    {
        var codes = book.Languages().Select(l => l.Code).ToList();
        var target = codes.IndexOf(DinoBook.Primary(to));
        var done = new List<TranslationResult>(work.Count);
        foreach (var job in work)
        {
            cancel.ThrowIfCancellationRequested();
            var forms = codes.Select(code => book.In(job.Source, code)).ToList();
            done.Add(new TranslationResult(job.Page, forms, target >= 0 ? forms[target] : book.In(job.Source, to)));
        }

        return done;
    }

    private void ApplyTranslated(List<TranslationResult> done)
    {
        var kept = ApplyForms(done);
        System.Diagnostics.Debug.WriteLine($"[language] switched pages={done.Count} keptOwnText={kept.Count}");
        ScheduleBookTextSave();
        Refresh();
        KeptTextNotice = KeptTextNoticeOf(kept);
    }

    /// <summary>
    /// Gives every page that is not user-edited the words of the target language and returns the pages that are. A page is user-edited only
    /// when its cleaned words match the book's words in none of its languages; a page already in the target language is left as it is.
    /// </summary>
    private List<BookPage> ApplyForms(List<TranslationResult> done)
    {
        var kept = new List<BookPage>();
        foreach (var result in done)
        {
            if (result.Page.Story is not { } story)
            {
                continue;
            }

            if (!result.Forms.Any(form => StoryLines.SameWords(story.Text, form)))
            {
                kept.Add(result.Page);
            }
            else if (!StoryLines.SameWords(story.Text, result.ToText))
            {
                SetStory(result.Page, story with { Text = result.ToText });
            }
        }

        return kept;
    }

    /// <summary>
    /// Opening a book puts the pages the user did not edit into the language the select shows, so the select never disagrees with the words.
    /// It runs through the same off-thread path and loading state as a switch; the disk text is not rewritten and no notice is made.
    /// </summary>
    private Task ReconcileLanguageOnOpen()
    {
        return _dino is not null && SelectedDinoLanguage.Length > 0
            ? ApplyLanguageAsync(SelectedDinoLanguage, SelectedDinoLanguage, reconcileOnly: true)
            : Task.CompletedTask;
    }

    private void ReconcileTranslated(List<TranslationResult> done)
    {
        ApplyForms(done);
        Refresh();
    }

    /// <summary>"Pages 3, 7 kept your own text." for the pages a language switch left alone, numbered as the Check step shows them; null when it changed every page.</summary>
    private string? KeptTextNoticeOf(List<BookPage> kept)
    {
        if (kept.Count == 0)
        {
            return null;
        }

        var covers = kept.Where(p => p.Kind != PageKind.Numbered).Select(TitleOf).ToList();
        var numbers = kept.Where(p => p.Kind == PageKind.Numbered).Select(p => _printedNumbers.TryGetValue(p, out var number) ? number : 0)
            .Where(number => number > 0).Order().Select(number => number.ToString(System.Globalization.CultureInfo.CurrentCulture)).ToList();
        var parts = new List<string>(covers);
        if (numbers.Count > 0)
        {
            parts.Add((numbers.Count == 1 ? KeptPageOne : KeptPageMany) + string.Join(", ", numbers));
        }

        return string.Join(", ", parts) + KeptTextTail;
    }

    private sealed record TranslationJob(BookPage Page, string Source);

    /// <summary><paramref name="Forms"/> is the page's source words in every language of the book; <paramref name="ToText"/> the one for the target.</summary>
    private sealed record TranslationResult(BookPage Page, IReadOnlyList<string> Forms, string ToText);

    /// <summary>The notice for an opened book: pages left out, a language chosen for them, and saved words that could not be carried over (an alternate folder without them).</summary>
    internal static string? NoticeOf(OpenedDino? dino, string? languageNotice)
    {
        var skipped = dino is { Book.SkippedPages: > 0 and var count } ? (count == 1 ? SkippedOneText : count + SkippedManyText) : null;
        var lost = dino is { SavedWordsKept: false } ? SavedWordsLostText : null;
        var notice = string.Join(' ', new[] { skipped, languageNotice, lost }.OfType<string>());
        return notice.Length > 0 ? notice : null;
    }
}

/// <summary>A valid Dino book none of whose languages the app can print; not a damaged file, so it is never deleted as one.</summary>
internal sealed class NoPrintableLanguageException(string message) : Exception(message);
