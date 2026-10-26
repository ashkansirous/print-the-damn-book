using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintTheDamnBook.App.Printing;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const string IdleStatus = "Open a folder of page images to begin.";
    private const string AllRemovedStatus = "You removed every page. Open a folder of page images to begin again.";
    private const string NoImagesStatus = "No jpg or png images found in this folder.";

    private readonly Func<string?> _pickFolder;
    private int _loadVersion;
    private PageList _pageList = new([]);
    private ThumbnailLoader _thumbnails = new();

    /// <summary>The blank filler that stands in for a missing back cover; a distinct instance so only that tile is flagged.</summary>
    private BookPage? _warnedBlank;

    /// <summary>The number printed on each page (see <see cref="PageNumbering.Compute"/>); covers and blanks have none.</summary>
    private IReadOnlyDictionary<BookPage, int> _printedNumbers = new Dictionary<BookPage, int>();

    /// <summary>1-based position of each real page, by identity, for the current book.</summary>
    private Dictionary<BookPage, int> _positions = new(ReferenceEqualityComparer.Instance);

    /// <summary>The print options the tiles were composed with (the story text depends on them).</summary>
    private StoryContext _storyContext;

    public MainViewModel(Func<string?> pickFolder, PrintViewModel print)
    {
        _pickFolder = pickFolder;
        Print = print;
        Print.PropertyChanged += (_, e) => OnPrintChanged(e.PropertyName);
        Print.BookletSent += (_, _) => Step = WizardStep.Done;
        InitStoryEditor();
        RefreshWizard();
    }

    public PrintViewModel Print { get; }

    /// <summary>Whether the As printed tiles show the number each page is printed with.</summary>
    public bool ShowsPageNumbers => Print.PageNumbers != PageNumberPosition.Off;

    [ObservableProperty]
    public partial IReadOnlyList<SpreadViewModel> Spreads { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SheetViewModel> Sheets { get; set; } = [];

    [ObservableProperty]
    public partial string FolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddPages))]
    public partial bool IsAdding { get; set; }

    /// <summary>Adding needs a loaded folder and no other add in flight.</summary>
    public bool CanAddPages => IsLoaded && !IsAdding;

    [ObservableProperty]
    public partial string Status { get; set; } = IdleStatus;

    /// <summary>Why the last folder could not be used (none found, unreadable); shown on the Open step.</summary>
    [ObservableProperty]
    public partial string? LoadProblem { get; set; }

    /// <summary>True while a folder is being read; the Open step shows a spinner.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (IsLoading)
        {
            return;
        }

        var folder = _pickFolder();
        if (folder is not null)
        {
            await LoadFolderAsync(folder);
        }
    }

    /// <summary>Loads a folder; if called again before it finishes, only the newest call updates the view.</summary>
    public async Task LoadFolderAsync(string folder)
    {
        var version = ++_loadVersion;
        IsLoading = true;
        try
        {
            var loaded = await Task.Run(() => ReadFolder(folder));
            if (version == _loadVersion)
            {
                Show(folder, loaded);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (version == _loadVersion)
            {
                ShowFailure(ex);
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    private static LoadedFolder ReadFolder(string folder)
    {
        var pages = PageOrdering.FromFolder(FolderImages.List(folder));
        var thumbnails = new ThumbnailLoader();
        thumbnails.Preload(pages.Select(p => p.Path));
        return new LoadedFolder(pages, thumbnails, BookTextStore.Read(folder));
    }

    private void ShowFailure(Exception ex)
    {
        ClearBook();
        Refresh();
        LoadProblem = $"Could not read this folder: {ex.Message}";
        Status = LoadProblem;
        Step = WizardStep.Open;
        RefreshWizard();
    }

    private void ClearBook()
    {
        CloseBookText();
        _pageList = new PageList([]);
        _thumbnails = new ThumbnailLoader();
        FolderPath = string.Empty;
        LoadProblem = null;
    }

    private void Show(string folder, LoadedFolder loaded)
    {
        CloseBookText();
        _pageList = new PageList(loaded.Pages);
        _thumbnails = loaded.Thumbnails;
        OpenBookText(folder, loaded.Text);
        FolderPath = folder;
        LoadProblem = _pageList.Pages.Count == 0 ? NoImagesStatus : null;
        Refresh();
        Step = IsLoaded ? WizardStep.Check : WizardStep.Open;
    }

    /// <summary>Recomputes the reading-order and sheet views, status and warnings from the current page list.</summary>
    private void Refresh()
    {
        _refreshing = true;
        try
        {
            RefreshBook();
        }
        finally
        {
            _refreshing = false;
        }

        RefreshWizard();
        UpdateEditor();
    }

    private void RefreshBook()
    {
        var pages = _pageList.Pages;
        var padded = MarkWarnedBlank(_pageList.Padded, pages);
        var sheets = Imposition.Impose(padded);
        var cache = new Dictionary<BookPage, PageViewModel>(ReferenceEqualityComparer.Instance);
        RefreshCoverTint(pages);
        _storyContext = CurrentStoryContext();
        ApplyPlainnessToAll(_storyContext);

        _printedNumbers = PageNumbering.Compute(padded);
        _positions = PositionsOf(pages);
        Spreads = PrintTheDamnBook.Core.Spreads.Of(padded).Select(s => ToViewModel(s, cache)).ToList();
        Sheets = sheets.Select(s => ToViewModel(s, cache)).ToList();
        Print.SetSheets(sheets, Path.GetFileName(Path.TrimEndingDirectorySeparator(FolderPath)), _printedNumbers);
        Status = LoadProblem ?? BuildStatus(pages.Count, sheets.Length, padded);
        IsLoaded = pages.Count > 0;
        Chips = BuildChips(BookChecks.Find(pages, sheets.Length), sheets.Length, FailedImages(pages), CountTooLong(pages, _storyContext));
        MarkSelectedTiles();
    }

    private static Dictionary<BookPage, int> PositionsOf(IReadOnlyList<BookPage> pages)
    {
        var positions = new Dictionary<BookPage, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < pages.Count; i++)
        {
            positions[pages[i]] = i + 1;
        }

        return positions;
    }

    /// <summary>With no back cover the last blank is the one the book ends on; swap in a distinct instance so it can be flagged.</summary>
    private IReadOnlyList<BookPage> MarkWarnedBlank(IReadOnlyList<BookPage> padded, IReadOnlyList<BookPage> pages)
    {
        _warnedBlank = null;
        if (pages.Count == 0 || pages[^1].Kind == PageKind.BackCover || padded[^1].Kind != PageKind.Blank)
        {
            return padded;
        }

        _warnedBlank = new BookPage(null, BookPage.BlankLabel, PageKind.Blank);
        return [.. padded.Take(padded.Count - 1), _warnedBlank];
    }

    /// <summary>Files among the current pages that could not be decoded, so the warning never outlives a removed page.</summary>
    private List<string> FailedImages(IReadOnlyList<BookPage> pages) =>
        pages.Select(p => p.Path)
            .OfType<string>()
            .Where(path => _thumbnails.Load(path) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private SpreadViewModel ToViewModel(Spread spread, Dictionary<BookPage, PageViewModel> cache)
    {
        var caption = spread.Kind switch
        {
            SpreadKind.FrontCover => "Front cover",
            SpreadKind.BackCover => "Back cover",
            _ => $"Spread {spread.Number}",
        };
        return new SpreadViewModel(caption.ToUpperInvariant(), ToStripItem(spread.Left, cache), ToStripItem(spread.Right, cache));
    }

    private PageViewModel? ToStripItem(BookPage? page, Dictionary<BookPage, PageViewModel> cache)
    {
        if (page is null)
        {
            return null;
        }

        var isFiller = page.Kind is PageKind.Blank or PageKind.Spacer;
        return ToViewModel(page, cache) with
        {
            Source = SourceOf(page),
            IsPinned = !isFiller && _pageList.IsPinned(page),
            Position = _positions.GetValueOrDefault(page),
            Total = _positions.Count,
        };
    }

    /// <summary>Moves <paramref name="page"/> to where <paramref name="target"/> is; refused moves change nothing.</summary>
    public void MovePage(BookPage page, BookPage target)
    {
        if (_pageList.Move(page, target))
        {
            Refresh();
        }
    }

    /// <summary>Moves <paramref name="page"/> after the last real page (before a back cover); refused moves change nothing.</summary>
    public void MovePageToEnd(BookPage page)
    {
        if (_pageList.MoveToEnd(page))
        {
            Refresh();
        }
    }

    /// <summary>Moves <paramref name="page"/> one place earlier (-1) or later (1); refused moves (covers, the ends) change nothing.</summary>
    public bool MovePageBy(BookPage page, int offset)
    {
        var pages = _pageList.Pages;
        var index = pages.ToList().FindIndex(p => ReferenceEquals(p, page));
        var target = index + offset;
        if (index < 0 || target < 0 || target >= pages.Count || !_pageList.Move(page, pages[target]))
        {
            return false;
        }

        Refresh();
        return true;
    }

    [RelayCommand]
    private void RemovePage(BookPage? page)
    {
        if (page is null || !_pageList.Remove(page))
        {
            return;
        }

        Refresh();
        if (!IsLoaded)
        {
            ReturnToOpenAfterLastRemoval();
        }
    }

    /// <summary>With no pages left there is nothing to check or print; go back to Open and say why.</summary>
    private void ReturnToOpenAfterLastRemoval()
    {
        LoadProblem = AllRemovedStatus;
        Status = AllRemovedStatus;
        Step = WizardStep.Open;
        RefreshWizard();
    }

    /// <summary>Adds image files before the back cover; unreadable ones are reported like at load time.</summary>
    public async Task AddPagesAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 || !CanAddPages)
        {
            return;
        }

        IsAdding = true;
        try
        {
            await AddPreloadedAsync(paths);
        }
        finally
        {
            IsAdding = false;
        }
    }

    private async Task AddPreloadedAsync(IReadOnlyList<string> paths)
    {
        var version = _loadVersion;
        var thumbnails = _thumbnails;
        await Task.Run(() => thumbnails.Preload(paths));
        if (version == _loadVersion && ReferenceEquals(thumbnails, _thumbnails))
        {
            _pageList.Add(paths);
            Refresh();
        }
    }

    /// <summary>Forgets the folder and returns to the Open step.</summary>
    private void StartOver()
    {
        _loadVersion++;
        IsLoading = false;
        ClearBook();
        Print.ClearStatus();
        Refresh();
        Status = IdleStatus;
        Step = WizardStep.Open;
    }

    private PageViewModel ToViewModel(BookPage page, Dictionary<BookPage, PageViewModel> cache) => ToViewModel(page, cache, _storyContext);

    /// <summary>The caption under a tile: the file's name, "Text for N" for a text page, "spacer" for a spacer.</summary>
    private static string LabelOf(BookPage page) => page.Kind switch
    {
        PageKind.Text => TextForPrefix + page.Label,
        PageKind.Spacer => SpacerLabel,
        _ => page.Label,
    };

    private PageViewModel ToViewModel(BookPage page, Dictionary<BookPage, PageViewModel> cache, StoryContext context)
    {
        if (!cache.TryGetValue(page, out var viewModel))
        {
            var image = TileImage(page, context);
            var hasImage = image is not null || page.Path is null;
            var (note, warned) = PageNotes.For(page, hasImage, ReferenceEquals(page, _warnedBlank));
            viewModel = new PageViewModel(LabelOf(page), image, page.Kind) { Note = note, IsWarned = warned, PrintedNumber = _printedNumbers.GetValueOrDefault(page) };
            cache[page] = viewModel;
        }

        return viewModel;
    }

    private SheetViewModel ToViewModel(Sheet sheet, Dictionary<BookPage, PageViewModel> cache) =>
        new($"Sheet {sheet.Number}", sheet.Number == 1, ToViewModel(sheet.Front, cache), ToViewModel(sheet.Back, cache));

    private FaceViewModel ToViewModel(SheetFace face, Dictionary<BookPage, PageViewModel> cache) =>
        new(ToSheetItem(face.Left, cache), ToSheetItem(face.Right, cache));

    /// <summary>A tile of the As printed view: clicking it selects its picture, but it cannot be dragged or removed there.</summary>
    private PageViewModel ToSheetItem(BookPage page, Dictionary<BookPage, PageViewModel> cache) =>
        ToViewModel(page, cache) with { Source = SourceOf(page) };

    /// <summary>The picture a tile stands for: the page itself, the picture of a text page, or null for filler.</summary>
    private BookPage? SourceOf(BookPage page) => page.Kind switch
    {
        PageKind.Blank or PageKind.Spacer => null,
        PageKind.Text => _pageList.PictureOf(page),
        _ => page,
    };

    private static string BuildStatus(int pageCount, int sheetCount, IReadOnlyList<BookPage> padded)
    {
        if (pageCount == 0)
        {
            return NoImagesStatus;
        }

        var textPages = padded.Count(p => p.Kind == PageKind.Text);
        var spacers = padded.Count(p => p.Kind == PageKind.Spacer);
        var extras = new[] { textPages > 0 ? Plural(textPages, "text page") : null, spacers > 0 ? $"{spacers} spacer" : null }.OfType<string>().ToList();
        return BuildSummary(pageCount, sheetCount) + (extras.Count > 0 ? ", " + string.Join(", ", extras) : string.Empty) + ".";
    }

    private static string BuildSummary(int pageCount, int sheetCount) =>
        $"{pageCount} {(pageCount == 1 ? "page" : "pages")} on {sheetCount} {(sheetCount == 1 ? "sheet" : "sheets")}";
}

internal sealed record LoadedFolder(IReadOnlyList<BookPage> Pages, ThumbnailLoader Thumbnails, BookTextLoad Text);
