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
    public partial string FolderPath { get; set; } = string.Empty;

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
        var failed = thumbnails.Preload(pages.Select(p => p.Path));
        return new LoadedFolder(pages, thumbnails, failed);
    }

    private void ShowFailure(Exception ex)
    {
        Pages = [];
        Sheets = [];
        Print.SetSheets([]);
        FolderPath = string.Empty;
        Warning = null;
        Status = $"Could not read this folder: {ex.Message}";
    }

    private void Show(string folder, LoadedFolder loaded)
    {
        var pages = loaded.Pages;
        var sheets = Imposition.Compute(pages);
        var cache = new Dictionary<BookPage, PageViewModel>();

        FolderPath = folder;
        Pages = pages.Select(p => ToViewModel(p, loaded.Thumbnails, cache)).ToList();
        Sheets = sheets.Select(s => ToViewModel(s, loaded.Thumbnails, cache)).ToList();
        Print.SetSheets(sheets, Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)));
        Status = BuildStatus(pages.Count, sheets.Length);
        Warning = BuildWarning(BookChecks.Find(pages, sheets.Length), sheets.Length, loaded.Failed);
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
            BookIssue.UnrecognisedFiles => $"These files have names the app does not recognise and were placed before the back cover: {JoinNames(finding.Files)}.",
            BookIssue.DuplicateCover => "More than one file is named Cover. Only the first is used; the others are placed with the unrecognised files.",
            BookIssue.DuplicateBackCover => "More than one file is named Back cover. Only the first is used; the others are placed with the unrecognised files.",
            _ => throw new ArgumentOutOfRangeException(nameof(finding), finding.Issue, null),
        };
    }
}

internal sealed record LoadedFolder(IReadOnlyList<BookPage> Pages, ThumbnailLoader Thumbnails, IReadOnlyList<string> Failed);
