using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintTheDamnBook.App.Printing;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.Core;

namespace PrintTheDamnBook.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly Func<string?> _pickFolder;
    private int _loadVersion;
    private PageList _pageList = new([]);
    private ThumbnailLoader _thumbnails = new();

    public MainViewModel(Func<string?> pickFolder, PrintViewModel print)
    {
        _pickFolder = pickFolder;
        Print = print;
    }

    public PrintViewModel Print { get; }

    [ObservableProperty]
    public partial IReadOnlyList<PageViewModel> Pages { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SheetViewModel> Sheets { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddPages))]
    public partial string FolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddPages))]
    public partial bool IsAdding { get; set; }

    /// <summary>Adding needs a loaded folder and no other add in flight.</summary>
    public bool CanAddPages => FolderPath.Length > 0 && !IsAdding;

    [ObservableProperty]
    public partial string Status { get; set; } = "Open a folder of page images to begin.";

    [ObservableProperty]
    public partial string? Warning { get; set; }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
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
    }

    private static LoadedFolder ReadFolder(string folder)
    {
        var pages = PageOrdering.FromFolder(FolderImages.List(folder));
        var thumbnails = new ThumbnailLoader();
        thumbnails.Preload(pages.Select(p => p.Path));
        return new LoadedFolder(pages, thumbnails);
    }

    private void ShowFailure(Exception ex)
    {
        _pageList = new PageList([]);
        _thumbnails = new ThumbnailLoader();
        Pages = [];
        Sheets = [];
        Print.SetSheets([]);
        FolderPath = string.Empty;
        Warning = null;
        Status = $"Could not read this folder: {ex.Message}";
    }

    private void Show(string folder, LoadedFolder loaded)
    {
        _pageList = new PageList(loaded.Pages);
        _thumbnails = loaded.Thumbnails;
        FolderPath = folder;
        Refresh();
    }

    /// <summary>Recomputes the page strip, sheet preview, status and warnings from the current page list.</summary>
    private void Refresh()
    {
        var pages = _pageList.Pages;
        var padded = _pageList.Padded;
        var sheets = Imposition.Impose(padded);
        var cache = new Dictionary<BookPage, PageViewModel>(ReferenceEqualityComparer.Instance);

        Pages = padded.Select(p => ToStripItem(p, cache)).ToList();
        Sheets = sheets.Select(s => ToViewModel(s, _thumbnails, cache)).ToList();
        Print.SetSheets(sheets, Path.GetFileName(Path.TrimEndingDirectorySeparator(FolderPath)));
        Status = BuildStatus(pages.Count, sheets.Length);
        Warning = BuildWarning(BookChecks.Find(pages, sheets.Length), sheets.Length, FailedImages(pages));
    }

    /// <summary>Files among the current pages that could not be decoded, so the warning never outlives a removed page.</summary>
    private List<string> FailedImages(IReadOnlyList<BookPage> pages) =>
        pages.Select(p => p.Path)
            .OfType<string>()
            .Where(path => _thumbnails.Load(path) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private PageViewModel ToStripItem(BookPage page, Dictionary<BookPage, PageViewModel> cache)
    {
        var isBlank = page.Kind == PageKind.Blank;
        return ToViewModel(page, _thumbnails, cache) with
        {
            Source = isBlank ? null : page,
            IsPinned = !isBlank && _pageList.IsPinned(page),
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

    [RelayCommand]
    private void RemovePage(BookPage? page)
    {
        if (page is not null && _pageList.Remove(page))
        {
            Refresh();
        }
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

    private static PageViewModel ToViewModel(BookPage page, ThumbnailLoader thumbnails, Dictionary<BookPage, PageViewModel> cache)
    {
        if (!cache.TryGetValue(page, out var viewModel))
        {
            viewModel = new PageViewModel(page.Label, thumbnails.Load(page.Path), page.Kind);
            cache[page] = viewModel;
        }

        return viewModel;
    }

    private static FaceViewModel ToViewModel(SheetFace face, ThumbnailLoader thumbnails, Dictionary<BookPage, PageViewModel> cache) =>
        new(ToViewModel(face.Left, thumbnails, cache), ToViewModel(face.Right, thumbnails, cache));

    private static SheetViewModel ToViewModel(Sheet sheet, ThumbnailLoader thumbnails, Dictionary<BookPage, PageViewModel> cache) =>
        new($"Sheet {sheet.Number}", ToViewModel(sheet.Front, thumbnails, cache), ToViewModel(sheet.Back, thumbnails, cache));

    private static string BuildStatus(int pageCount, int sheetCount)
    {
        if (pageCount == 0)
        {
            return "No jpg or png images found in this folder.";
        }

        return $"{pageCount} {(pageCount == 1 ? "page" : "pages")} on {sheetCount} {(sheetCount == 1 ? "sheet" : "sheets")}.";
    }

    private static string? BuildWarning(IReadOnlyList<BookFinding> issues, int sheetCount, IReadOnlyList<string> failedImages)
    {
        var lines = issues.Select(finding => Describe(finding, sheetCount)).ToList();
        if (failedImages.Count > 0)
        {
            var names = string.Join(", ", failedImages.Select(Path.GetFileName));
            lines.Add($"These files could not be read as images and show blank: {names}.");
        }

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static string JoinNames(IReadOnlyList<string> names) => string.Join(", ", names);

    private static string Describe(BookFinding finding, int sheetCount)
    {
        return finding.Issue switch
        {
            BookIssue.TooThick => $"This book needs {sheetCount} sheets. More than {Imposition.MaxFlatSheets} is too thick to fold flat.",
            BookIssue.MissingCover => "No file named Cover was found, so the first page is printed as the front cover.",
            BookIssue.MissingBackCover => "No Back cover file found; the back of the book will be blank.",
            BookIssue.UnrecognisedFiles => $"These files have names the app does not recognise: {JoinNames(finding.Files)}.",
            BookIssue.DuplicateCover => "More than one file is named Cover. Only the first is used; the others are placed with the unrecognised files.",
            BookIssue.DuplicateBackCover => "More than one file is named Back cover. Only the first is used; the others are placed with the unrecognised files.",
            _ => throw new ArgumentOutOfRangeException(nameof(finding), finding.Issue, null),
        };
    }
}

internal sealed record LoadedFolder(IReadOnlyList<BookPage> Pages, ThumbnailLoader Thumbnails);
