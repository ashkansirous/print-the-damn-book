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
    private const string SavedText = "Saved to book.json in the folder";
    private const string SavingText = "Saving…";
    private const string FailedText = "Couldn't save book.json in this folder.";
    private const string LockedText = "book.json in this folder is from a newer version, so I left it alone.";
    private const string TooLongChipOne = "1 page has more words than fit";
    private const string TooLongChipMany = " pages have more words than fit";
    private const double PreviewSingleWidth = 212;
    private const double PreviewPairWidth = 128;
    private const double PreviewHeightRatio = 210.0 / 148.0;
    private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(250);

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
    private BookTextStore? _bookText;
    private Dictionary<string, PageTextEntry> _textEntries = new(StringComparer.OrdinalIgnoreCase);
    private string _bookTextFolder = string.Empty;
    private BookPage? _filledPage;
    private bool _syncingEditor;
    private bool _loadingBookText;
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

        SetStory(page, string.IsNullOrWhiteSpace(text) ? null : new StoryText(text, SelectedPlacement));
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

    /// <summary>Gives a page its words (or takes them away), remembers them for <c>book.json</c> and schedules the save.</summary>
    private void SetStory(BookPage page, StoryText? story)
    {
        page.Story = StoryText.OrNull(story);
        if (page.Path is { } path)
        {
            var key = BookTextJson.KeyFor(_bookTextFolder, path);
            if (page.Story is { } kept)
            {
                _textEntries[key] = new PageTextEntry(kept.Text, kept.Placement);
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
                    page.Story = new StoryText(entry.Text, entry.Placement);
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
    }

    private void CloseBookText()
    {
        _typingTimer.Stop();
        _bookText?.Dispose();
        _bookText = null;
        _textEntries = new Dictionary<string, PageTextEntry>(StringComparer.OrdinalIgnoreCase);
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
        AtFirstPage = index <= 0;
        AtLastPage = index >= pages.Count - 1;
        FillFit(page, context);
        FillPreview(page, context);
        PlacementNote = PlacementNoteOf(page);
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
