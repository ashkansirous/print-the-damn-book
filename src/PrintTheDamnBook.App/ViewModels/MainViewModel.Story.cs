using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.App.ViewModels;

/// <summary>How the words on the selected page fit: shown with an icon in the editor.</summary>
public enum FitTone
{
    Fits,
    Shrunk,
    TooLong,
}

/// <summary>The story text editor of the Check step (Slice 14): which page is selected, its words and placement, the fit line and the save status.</summary>
public sealed partial class MainViewModel
{
    private const string FrontCoverTitle = "Front cover";
    private const string BackCoverTitle = "Back cover";
    private const string PageTitlePrefix = "Page ";
    private const string TextForPrefix = "Text for ";
    private const string SpacerLabel = "spacer";
    private const string PlacementOnPicture = "A soft panel over the picture, never more than 30% of it.";
    private const string PlacementOwnPage = "Text on the left, picture on the right, facing each other when the book is open.";
    private const string PlacementOwnPageSpacer = "Text on the left, picture on the right. I put a spacer page in front so they face each other.";
    private const string PlacementCover = "Covers keep their words on the picture.";
    private const string BandPickedHint = "Your pick. I'll never suggest over it.";
    private const string BandSuggestedHintPrefix = "Suggested: the ";
    private const string BandSuggestedHintSuffix = " is the plainest part of this picture. Pick another and it sticks.";
    private const string SavedText = "Saved to book.json in the folder";
    private const string SavingText = "Saving…";
    private const string FailedText = "Couldn't save book.json in this folder.";
    private const string LockedText = "book.json in this folder is from a newer version, so I left it alone.";
    private const string TooLongChipOne = "1 page has more words than fit";
    private const string TooLongChipMany = " pages have more words than fit";
    private const double PreviewSingleWidth = 212;
    private const double PreviewPairWidth = 128;
    private const double PreviewHeightRatio = 210.0 / 148.0;
    private const string ReadingPicturesText = "Reading pictures for the text bands…";
    private static readonly TimeSpan SlowWait = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan SampleRefreshDelay = TimeSpan.FromMilliseconds(300);

    private static readonly HashSet<string> StoryLayoutProperties =
    [
        nameof(Printing.PrintViewModel.SelectedPaper),
        nameof(Printing.PrintViewModel.MarginMm),
        nameof(Printing.PrintViewModel.Scaling),
        nameof(Printing.PrintViewModel.PageNumbers),
        nameof(Printing.PrintViewModel.TextPages),
        nameof(Printing.PrintViewModel.CoverTint),
    ];

    private readonly DispatcherTimer _typingTimer = new() { Interval = TypingDelay };

    /// <summary>Coalesces the finished picture reads into one refresh, so 40 reads rebuild the book once, not 40 times.</summary>
    private readonly DispatcherTimer _sampleTimer = new() { Interval = SampleRefreshDelay };
    private BookTextStore? _bookText;
    private Dictionary<string, PageTextEntry> _textEntries = new(StringComparer.OrdinalIgnoreCase);
    private string _bookTextFolder = string.Empty;
    private BookPage? _filledPage;
    private bool _syncingEditor;
    private bool _loadingBookText;

    /// <summary>The pixels of each picture that was looked at (by <c>book.json</c> key), read off the UI thread; null when the picture could not be read, so it is not tried again. Never saved.</summary>
    private readonly Dictionary<string, PictureSample?> _samples = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How plain each band of the part of a picture that shows on the page is; the part depends on the paper, margin and scaling.</summary>
    private readonly Dictionary<(string Key, PictureWindow Window), BandPlainness?> _plainness = [];

    /// <summary>The band the user picked for each picture (by key), kept when its words are cleared so retyping gets the pick back.</summary>
    private readonly Dictionary<string, TextBand> _bandPicks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Pictures being read right now (by key), so one is never read twice at once and a print can wait for them.</summary>
    private readonly Dictionary<string, Task> _measuring = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Bumped when a folder closes, so a measurement that finishes late is dropped.</summary>
    private int _measureVersion;
    private Rgb? _coverTint;
    private string? _coverTintPath;

    /// <summary>True once the cover at <see cref="_coverTintPath"/> has been read (even when it gave no tint), so Refresh does not read it again.</summary>
    private bool _coverTintRead;

    /// <summary>The picture whose words are being edited (a text page selects its picture); null when none is.</summary>
    public BookPage? SelectedPage { get; private set; }

    public bool HasSelection => SelectedPage is not null;

    public bool HasNoSelection => SelectedPage is null;

    [ObservableProperty]
    public partial string SelectedTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedFile { get; set; } = string.Empty;

    /// <summary>The Story text box; every change is applied to the page at once and the tiles follow a moment later.</summary>
    [ObservableProperty]
    public partial string StoryInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial TextPlacement SelectedPlacement { get; set; } = TextPlacement.OnPicture;

    [ObservableProperty]
    public partial bool HasStoryText { get; set; }

    /// <summary>The band the panel sits in (the pick, else the suggestion, else the bottom).</summary>
    [ObservableProperty]
    public partial TextBand SelectedBand { get; set; } = TextBand.Bottom;

    /// <summary>True for words over a picture: the Band control is shown only then.</summary>
    [ObservableProperty]
    public partial bool ShowBandPicker { get; set; }

    /// <summary>The plainest band once the picture has been looked at; null until then.</summary>
    [ObservableProperty]
    public partial TextBand? SuggestedBand { get; set; }

    /// <summary>True when the user has picked a band: the hint then says it sticks.</summary>
    [ObservableProperty]
    public partial bool BandIsPicked { get; set; }

    [ObservableProperty]
    public partial string BandHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasBandHint { get; set; }

    /// <summary>False for a cover, which keeps its words on the picture.</summary>
    [ObservableProperty]
    public partial bool CanChooseOwnPage { get; set; } = true;

    [ObservableProperty]
    public partial string PlacementNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FitNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial FitTone FitTone { get; set; }

    [ObservableProperty]
    public partial bool HasFitNote { get; set; }

    /// <summary>One page, or the text page and its picture side by side for Own page.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PageViewModel> PreviewTiles { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewTileHeight))]
    public partial double PreviewTileWidth { get; set; } = PreviewSingleWidth;

    /// <summary>The preview tiles have the proportions of a half sheet (A4: 148 x 210).</summary>
    public double PreviewTileHeight => PreviewTileWidth * PreviewHeightRatio;

    [ObservableProperty]
    public partial bool AtFirstPage { get; set; } = true;

    [ObservableProperty]
    public partial bool AtLastPage { get; set; } = true;

    [ObservableProperty]
    public partial string SaveText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSavingText { get; set; }

    private void InitStoryEditor()
    {
        _typingTimer.Tick += (_, _) => OnTypingPaused();
        if (Application.Current is { } app)
        {
            app.Exit += (_, _) => _bookText?.Flush();
        }

        Print.PropertyChanged += (_, e) => OnPrintChangedForStory(e.PropertyName);
        Print.BeforePrint = SettleBandsAsync;
        _sampleTimer.Tick += (_, _) => OnSamplesRead();
    }

    partial void OnStoryInputChanged(string value)
    {
        if (!_syncingEditor)
        {
            ApplyTypedText(value);
        }
    }

    partial void OnSelectedPlacementChanged(TextPlacement value)
    {
        if (!_syncingEditor)
        {
            ApplyPlacement(value);
        }
    }

    partial void OnSelectedBandChanged(TextBand value)
    {
        if (!_syncingEditor)
        {
            ApplyBand(value);
        }
    }

    /// <summary>Selects the picture a tile belongs to (a text page selects its picture); a filler tile changes nothing.</summary>
    public void SelectTile(PageViewModel? tile)
    {
        if (tile?.Source is { } page)
        {
            SelectPage(page);
        }
    }

    private void SelectPage(BookPage? page)
    {
        if (_typingTimer.IsEnabled)
        {
            OnTypingPaused();
        }

        SelectedPage = page;
        if (page is not null)
        {
            EnsureSample(page);
        }

        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
        MarkSelectedTiles();
        UpdateEditor();
    }

    [RelayCommand]
    private void SelectPrevious() => SelectNeighbour(-1);

    [RelayCommand]
    private void SelectNext() => SelectNeighbour(1);

    private void SelectNeighbour(int offset)
    {
        var pages = _pageList.Pages;
        var index = SelectedPage is null ? -1 : IndexOfPage(pages, SelectedPage) + offset;
        if (index >= 0 && index < pages.Count)
        {
            SelectPage(pages[index]);
        }
    }

    [RelayCommand]
    private void RemoveStoryText()
    {
        if (SelectedPage is null)
        {
            return;
        }

        SetStory(SelectedPage, null);
        Refresh();
    }

    private void ApplyTypedText(string text)
    {
        if (SelectedPage is not { } page)
        {
            return;
        }

        var story = page.Story is { } current ? current with { Text = text, Placement = SelectedPlacement } : new StoryText(text, SelectedPlacement, PickedBandOf(page));
        SetStory(page, string.IsNullOrWhiteSpace(text) ? null : story);
        HasStoryText = page.Story is not null;
        _typingTimer.Stop();
        _typingTimer.Start();
    }

    private void OnTypingPaused()
    {
        _typingTimer.Stop();
        Refresh();
    }

    private void ApplyPlacement(TextPlacement placement)
    {
        if (SelectedPage is not { Story: { } story } page || story.Placement == placement)
        {
            return;
        }

        SetStory(page, story with { Placement = placement });
        Refresh();
    }

    private void ApplyBand(TextBand band)
    {
        if (SelectedPage is not { Story: { } story } page || story.Band == band)
        {
            return;
        }

        SetStory(page, story with { Band = band });
        Refresh();
    }

    /// <summary>Gives a page its words (or takes them away), remembers them for <c>book.json</c> and schedules the save.</summary>
    private void SetStory(BookPage page, StoryText? story)
    {
        var gainedWords = page.Story is null;
        page.Story = StoryText.OrNull(story);
        if (KeyOf(page) is { } key)
        {
            if (page.Story is { } kept)
            {
                _textEntries[key] = new PageTextEntry(kept.Text, kept.Placement, kept.Band);
                RememberPick(key, kept);
                if (gainedWords)
                {
                    EnsureSample(page);
                    ApplyPlainness(page, CurrentStoryContext());
                }
            }
            else
            {
                _textEntries.Remove(key);
            }
        }

        ScheduleBookTextSave();
    }

    private void ScheduleBookTextSave()
    {
        if (_loadingBookText || _bookText is null)
        {
            return;
        }

        _bookText.Schedule(new BookText { TextPages = Print.TextPages, Pages = new Dictionary<string, PageTextEntry>(_textEntries, StringComparer.OrdinalIgnoreCase) });
        UpdateSaveStatus();
    }

    private void OnBookTextStateChanged()
    {
        UpdateSaveStatus();
    }

    private void UpdateSaveStatus()
    {
        var state = _bookText?.State ?? BookTextSaveState.Idle;
        IsSavingText = state == BookTextSaveState.Saving;
        SaveText = state switch
        {
            BookTextSaveState.Saving => SavingText,
            BookTextSaveState.Failed => FailedText,
            BookTextSaveState.Locked => LockedText,
            BookTextSaveState.Saved => SavedText,
            _ => _textEntries.Count > 0 ? SavedText : string.Empty,
        };
    }

    /// <summary>Opens the folder's saved words: gives each page its text, restores the text-page paper and starts the store.</summary>
    private void OpenBookText(string folder, BookTextLoad load)
    {
        _loadingBookText = true;
        _bookTextFolder = folder;
        try
        {
            _textEntries = new Dictionary<string, PageTextEntry>(load.Book.Pages, StringComparer.OrdinalIgnoreCase);
            foreach (var page in _pageList.Pages)
            {
                if (page.Path is { } path && _textEntries.TryGetValue(BookTextJson.KeyFor(folder, path), out var entry))
                {
                    page.Story = new StoryText(entry.Text, entry.Placement, entry.Band);
                    RememberPick(BookTextJson.KeyFor(folder, path), page.Story);
                }
            }

            Print.TextPages = load.Book.TextPages;
        }
        finally
        {
            _loadingBookText = false;
        }

        _bookText = new BookTextStore(folder, load.CanSave);
        _bookText.StateChanged += (_, _) => OnBookTextStateChanged();
        UpdateSaveStatus();
        MeasureStoryPictures();
    }

    private void MeasureStoryPictures()
    {
        foreach (var page in _pageList.Pages)
        {
            if (page.Story is not null)
            {
                EnsureSample(page);
            }
        }
    }

    private string? KeyOf(BookPage page) => page.Path is { } path ? BookTextJson.KeyFor(_bookTextFolder, path) : null;

    private TextBand? PickedBandOf(BookPage page) => KeyOf(page) is { } key && _bandPicks.TryGetValue(key, out var band) ? band : null;

    private void RememberPick(string key, StoryText story)
    {
        if (story.Band is { } band)
        {
            _bandPicks[key] = band;
        }
    }

    /// <summary>Starts reading the page's picture off the UI thread, unless that was done or is under way.</summary>
    private void EnsureSample(BookPage page)
    {
        if (page.Path is not { } path || KeyOf(page) is not { } key || _samples.ContainsKey(key) || _measuring.ContainsKey(key))
        {
            return;
        }

        _measuring[key] = ReadSampleAsync(key, path, _measureVersion);
    }

    private async Task ReadSampleAsync(string key, string path, int version)
    {
        var thumbnails = _thumbnails;
        PictureSample? sample = null;
        try
        {
            sample = await Task.Run(() => BandAnalyzer.Sample(thumbnails.Load(path)));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An odd bitmap gives no suggestion: the page keeps the bottom band and the picture is not read again.
            Trace.TraceWarning($"Could not read {path} for the text band suggestion: {ex}");
        }

        if (version != _measureVersion)
        {
            return;
        }

        _measuring.Remove(key);
        _samples[key] = sample;
        if (!_sampleTimer.IsEnabled)
        {
            _sampleTimer.Start();
        }
    }

    /// <summary>Shows the plainness of the pictures read since the last time; waits while a print runs, and leaves it to the typing pause while the user types.</summary>
    private void OnSamplesRead()
    {
        if (Print.IsBusy)
        {
            return;
        }

        _sampleTimer.Stop();
        if (!_typingTimer.IsEnabled)
        {
            Refresh();
        }
    }

    /// <summary>Gives the page the plainness of the part of its picture that shows on the page, once the picture has been read.</summary>
    private bool ApplyPlainness(BookPage page, StoryContext context)
    {
        if (page.Story is not { } story || KeyOf(page) is not { } key || !_samples.TryGetValue(key, out var sample) || sample is null)
        {
            return false;
        }

        var plainness = PlainnessOf(key, sample, context);
        if (plainness == story.Plainness)
        {
            return false;
        }

        page.Story = story with { Plainness = plainness };
        return true;
    }

    private BandPlainness? PlainnessOf(string key, PictureSample sample, StoryContext context)
    {
        var window = StoryTiles.VisibleWindow(new SizeD(sample.Width, sample.Height), context);
        if (!_plainness.TryGetValue((key, window), out var plainness))
        {
            plainness = BandAnalyzer.Analyse(sample, window);
            _plainness[(key, window)] = plainness;
        }

        return plainness;
    }

    private bool ApplyPlainnessToAll(StoryContext context)
    {
        var changed = false;
        foreach (var page in _pageList.Pages)
        {
            changed |= ApplyPlainness(page, context);
        }

        return changed;
    }

    /// <summary>
    /// Before a print: waits for every picture with words to be read, so the bands and panels printed are the settled suggestion.
    /// Shows what it waits for in the status line; <paramref name="cancel"/> stops the wait. Rebuilds the preview when asked and the plainness changed.
    /// </summary>
    private async Task SettleBandsAsync(bool refreshPreview, CancellationToken cancel)
    {
        foreach (var page in _pageList.Pages.Where(p => p.HasBandText))
        {
            EnsureSample(page);
        }

        if (_measuring.Count > 0)
        {
            await WaitForReadsAsync(cancel);
        }

        var changed = ApplyPlainnessToAll(CurrentStoryContext());
        if (changed && refreshPreview)
        {
            Refresh();
        }
    }

    /// <summary>Waits for the running reads; past <see cref="SlowWait"/> it says what it waits for and offers Cancel.</summary>
    private async Task WaitForReadsAsync(CancellationToken cancel)
    {
        var reads = Task.WhenAll(_measuring.Values.ToArray());
        try
        {
            await reads.WaitAsync(SlowWait, cancel);
        }
        catch (TimeoutException)
        {
            Print.SetStatus(ReadingPicturesText, Printing.PrintStatusKind.Info);
            Print.IsSettling = true;
            await reads.WaitAsync(cancel);
        }
    }

    private void CloseBookText()
    {
        _typingTimer.Stop();
        _sampleTimer.Stop();
        _bookText?.Dispose();
        _bookText = null;
        _textEntries = new Dictionary<string, PageTextEntry>(StringComparer.OrdinalIgnoreCase);
        _samples.Clear();
        _plainness.Clear();
        _bandPicks.Clear();
        _measuring.Clear();
        _measureVersion++;
        _coverTintRead = false;
        _coverTint = null;
        _coverTintPath = null;
        SelectedPage = null;
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
        UpdateEditor();
    }

    private void OnPrintChangedForStory(string? property)
    {
        if (property == nameof(Printing.PrintViewModel.TextPages))
        {
            ScheduleBookTextSave();
        }

        if (_refreshing || _loadingBookText || property is null || !StoryLayoutProperties.Contains(property) || !_pageList.Pages.Any(p => p.Story is not null))
        {
            return;
        }

        Refresh();
    }

    private StoryContext CurrentStoryContext()
    {
        var tint = Print.TextPages == TextPageBackground.TintedFromCover ? _coverTint : null;
        return new StoryContext(Print.SelectedPaper, Print.MarginMm, Print.Scaling, Print.PageNumbers, tint);
    }

    /// <summary>Reads the cover's colour when the cover changes and tells the print view model.</summary>
    private void RefreshCoverTint(IReadOnlyList<BookPage> pages)
    {
        var cover = pages.Count > 0 && pages[0].Kind == PageKind.Cover ? pages[0] : null;
        if (!_coverTintRead || cover?.Path != _coverTintPath)
        {
            _coverTintRead = true;
            _coverTintPath = cover?.Path;
            _coverTint = cover is null ? null : StoryTiles.TintOf(_thumbnails.Load(cover.Path));
        }

        Print.SetCoverTint(_coverTint);
    }

    private ImageSource? TileImage(BookPage page, StoryContext context)
    {
        if (page.Kind == PageKind.Text)
        {
            return StoryTiles.TextPage(page, context);
        }

        var thumbnail = _thumbnails.Load(page.Path);
        return StoryTiles.Picture(thumbnail as System.Windows.Media.Imaging.BitmapSource, page, context) ?? thumbnail;
    }

    private int CountTooLong(IReadOnlyList<BookPage> pages, StoryContext context)
    {
        return pages.Count(p => p.Story is not null && FitOf(p, context) is { Fits: false });
    }

    private TextFitResult? FitOf(BookPage page, StoryContext context)
    {
        var thumbnail = _thumbnails.Load(page.Path) as System.Windows.Media.Imaging.BitmapSource;
        var size = thumbnail is null ? (SizeD?)null : new SizeD(thumbnail.PixelWidth, thumbnail.PixelHeight);
        return StoryTiles.FitOf(page, size, context);
    }

    private void MarkSelectedTiles()
    {
        foreach (var tile in Spreads.SelectMany(s => new[] { s.Left, s.Right }).Concat(Sheets.SelectMany(s => new[] { s.Front.Left, s.Front.Right, s.Back.Left, s.Back.Right })))
        {
            if (tile is not null)
            {
                tile.IsSelected = SelectedPage is not null && tile.IsSelectable && ReferenceEquals(tile.Source, SelectedPage);
            }
        }
    }

    /// <summary>Brings the editor panel in line with the selected page and the current tiles.</summary>
    private void UpdateEditor()
    {
        if (SelectedPage is { } page && IndexOfPage(_pageList.Pages, page) < 0)
        {
            SelectedPage = null;
            OnPropertyChanged(nameof(SelectedPage));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasNoSelection));
        }

        _syncingEditor = true;
        try
        {
            FillEditor(SelectedPage);
        }
        finally
        {
            _syncingEditor = false;
        }

        UpdateSaveStatus();
    }

    private void FillEditor(BookPage? page)
    {
        if (page is null)
        {
            _filledPage = null;
            StoryInput = string.Empty;
            HasStoryText = false;
            ShowBandPicker = false;
            PreviewTiles = [];
            HasFitNote = false;
            return;
        }

        var pages = _pageList.Pages;
        var index = IndexOfPage(pages, page);
        var context = CurrentStoryContext();
        SelectedTitle = TitleOf(page);
        SelectedFile = page.Path is null ? string.Empty : Path.GetFileName(page.Path);
        FillStoryInput(page);
        HasStoryText = page.Story is not null;
        SelectedPlacement = page.Story is null ? TextPlacement.OnPicture : page.EffectivePlacement ?? TextPlacement.OnPicture;
        CanChooseOwnPage = page.CanHaveOwnPage;
        FillBand(page);
        AtFirstPage = index <= 0;
        AtLastPage = index >= pages.Count - 1;
        FillFit(page, context);
        FillPreview(page, context);
        PlacementNote = PlacementNoteOf(page);
    }

    private void FillBand(BookPage page)
    {
        var story = page.Story;
        ShowBandPicker = page.HasBandText;
        SelectedBand = story?.EffectiveBand ?? TextBand.Bottom;
        SuggestedBand = story?.SuggestedBand;
        BandIsPicked = story?.Band is not null;
        BandHint = BandHintOf(story);
        HasBandHint = BandHint.Length > 0;
    }

    private static string BandHintOf(StoryText? story)
    {
        if (story?.Band is not null)
        {
            return BandPickedHint;
        }

        return story?.SuggestedBand is { } suggested
            ? BandSuggestedHintPrefix + suggested.ToString().ToLowerInvariant() + BandSuggestedHintSuffix
            : string.Empty;
    }

    /// <summary>Shows the page's words; whitespace the user just typed on this same page stays in the box even though the model drops it.</summary>
    private void FillStoryInput(BookPage page)
    {
        var keepTyped = ReferenceEquals(_filledPage, page) && page.Story is null && string.IsNullOrWhiteSpace(StoryInput);
        _filledPage = page;
        if (!keepTyped)
        {
            StoryInput = page.Story?.Text ?? string.Empty;
        }
    }

    private void FillFit(BookPage page, StoryContext context)
    {
        if (FitOf(page, context) is not { } fit)
        {
            HasFitNote = false;
            return;
        }

        var own = page.HasOwnPageText;
        var where = own ? "its page" : "the band";
        var pt = FormatPoints(fit.Pt);
        var advice = own || !page.CanHaveOwnPage ? "Cut it down." : "Cut it down, or give it its own page.";
        HasFitNote = true;
        FitTone = !fit.Fits ? FitTone.TooLong : fit.Shrunk ? FitTone.Shrunk : FitTone.Fits;
        FitNote = !fit.Fits
            ? $"Too long for {where}, even at {pt} pt. {advice}"
            : fit.Shrunk ? $"Shrunk to {pt} pt so it fits {where}." : $"{pt} pt, fits {where}.";
    }

    private static string FormatPoints(double pt) => pt.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    private void FillPreview(BookPage page, StoryContext context)
    {
        var cache = new Dictionary<BookPage, PageViewModel>(ReferenceEqualityComparer.Instance);
        var textPage = page.HasOwnPageText ? _pageList.Expanded().FirstOrDefault(p => p.Kind == PageKind.Text && ReferenceEquals(_pageList.PictureOf(p), page)) : null;
        PageViewModel[] tiles = textPage is null
            ? [PreviewTile(page, cache, context)]
            : [PreviewTile(textPage, cache, context), PreviewTile(page, cache, context)];
        PreviewTileWidth = textPage is null ? PreviewSingleWidth : PreviewPairWidth;
        PreviewTiles = tiles;
    }

    private PageViewModel PreviewTile(BookPage page, Dictionary<BookPage, PageViewModel> cache, StoryContext context)
    {
        return ToViewModel(page, cache, context) with { Source = null };
    }

    private string PlacementNoteOf(BookPage page)
    {
        if (!page.CanHaveOwnPage)
        {
            return PlacementCover;
        }

        if (!page.HasOwnPageText)
        {
            return PlacementOnPicture;
        }

        var expanded = _pageList.Expanded();
        var at = expanded.ToList().FindIndex(p => ReferenceEquals(p, page));
        return at >= 2 && expanded[at - 2].Kind == PageKind.Spacer ? PlacementOwnPageSpacer : PlacementOwnPage;
    }

    private static string TitleOf(BookPage page) => page.Kind switch
    {
        PageKind.Cover => FrontCoverTitle,
        PageKind.BackCover => BackCoverTitle,
        PageKind.Numbered => PageTitlePrefix + page.Label,
        _ => page.Label,
    };

    private static int IndexOfPage(IReadOnlyList<BookPage> pages, BookPage page)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            if (ReferenceEquals(pages[i], page))
            {
                return i;
            }
        }

        return -1;
    }
}
