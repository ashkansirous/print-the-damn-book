using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintTheDamnBook.Core;

namespace PrintTheDamnBook.App.ViewModels;

/// <summary>The Open, Check, Print, Done flow: which step is showing, the rail, headings, footer and the step-specific text.</summary>
public sealed partial class MainViewModel
{
    private const string AppTitle = "Print the damn book";
    private const int RailStepCount = 3;
    private const int MaxStackLayers = 5;
    private const double StackLayerX = 7;
    private const double StackLayerY = 6;
    private static readonly string[] RailTitles = ["Open pages", "Check the order", "Print"];
    private const string NotLoadedFolderSummary = "A folder of images";
    private const string NotLoadedCheckSummary = "Drag to fix";
    private const string SentSummary = "Sent";
    private const int PrintRailIndex = 2;
    private const string NoPrinterName = "No printer";
    private const string TitleSuffixPrefix = "· ";
    private const string HeadingOpenLoaded = "Got your pages";
    private const string SubheadingOpenLoaded = "Happy with this folder? Next you check the order.";
    private const string HeadingOpen = "Point me at your pages";
    private const string SubheadingOpen = "Name them Cover, 1, 2, 3 … Back cover and I'll work out which page goes where on each sheet.";
    private const string HeadingCheck = "Check the order";
    private const string HeadingPrint = "Send it to the printer";
    private const string HeadingDone = "Sent. Now fold the damn thing.";
    private const string SubheadingDone = "Three steps and it's a book.";
    private const string SubheadingReading = "This is how the book reads. Drag a page to move it, or focus it and press Alt+Left or Alt+Right; the covers stay at the ends.";
    private const string SubheadingSheets = "This is what comes out of the printer. Each sheet prints both sides; fold the stack and it reads in order.";
    private const string LeadManual = "Two passes, fronts then backs.";
    private const string LeadAutomatic = "One job, double-sided.";
    private const string NextOpenLabel = "Next: check the order";
    private const string NextCheckLabel = "Next: printing";
    private const string NextPrintLabel = "Print the damn book";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpenStep))]
    [NotifyPropertyChangedFor(nameof(IsCheckStep))]
    [NotifyPropertyChangedFor(nameof(IsPrintStep))]
    [NotifyPropertyChangedFor(nameof(IsDoneStep))]
    public partial WizardStep Step { get; set; } = WizardStep.Open;

    public bool IsOpenStep => Step == WizardStep.Open;

    public bool IsCheckStep => Step == WizardStep.Check;

    public bool IsPrintStep => Step == WizardStep.Print;

    public bool IsDoneStep => Step == WizardStep.Done;

    /// <summary>Reading order (spreads) or As printed (sheets) on the Check step.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadingView))]
    [NotifyPropertyChangedFor(nameof(IsSheetsView))]
    public partial BookView View { get; set; } = BookView.Reading;

    public bool IsReadingView => View == BookView.Reading;

    public bool IsSheetsView => View == BookView.Sheets;

    /// <summary>True when a folder with at least one page is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddPages))]
    [NotifyPropertyChangedFor(nameof(IsNotLoaded))]
    public partial bool IsLoaded { get; set; }

    public bool IsNotLoaded => !IsLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChips))]
    public partial IReadOnlyList<string> Chips { get; set; } = [];

    public bool HasChips => Chips.Count > 0;

    /// <summary>True while the Print step's More options section is open; the test-sheet text only names controls that are showing.</summary>
    [ObservableProperty]
    public partial bool IsMoreOptionsOpen { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<RailStepViewModel> RailSteps { get; set; } = [];

    [ObservableProperty]
    public partial string Heading { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Subheading { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FolderName { get; set; } = string.Empty;

    /// <summary>The title bar suffix, "· folder name", or empty with no folder open.</summary>
    [ObservableProperty]
    public partial string TitleSuffix { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ImageSource? CoverImage { get; set; }

    /// <summary>"12 pages on 3 sheets" without the full stop, for the rail.</summary>
    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FooterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial FooterTone FooterTone { get; set; }

    [ObservableProperty]
    public partial bool HasBack { get; set; }

    [ObservableProperty]
    public partial bool HasNext { get; set; } = true;

    [ObservableProperty]
    public partial string NextLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool NextIsPrint { get; set; }

    [ObservableProperty]
    public partial string TestSheetText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StackTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<StackLayerViewModel> StackLayers { get; set; } = [];

    [ObservableProperty]
    public partial ImageSource? StackLeft { get; set; }

    [ObservableProperty]
    public partial ImageSource? StackRight { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<FoldStepViewModel> FoldSteps { get; set; } = [];

    /// <summary>True while a folder is read or a job is sent; the rail and the footer buttons wait.</summary>
    public bool IsBusy => IsLoading || Print.IsBusy;

    private int SheetCount => Sheets.Count;

    private int PageCount => Spreads.Sum(s => (s.Left?.IsBlank == false ? 1 : 0) + (s.Right?.IsBlank == false ? 1 : 0));

    partial void OnStepChanged(WizardStep value)
    {
        RefreshWizard();
    }

    partial void OnViewChanged(BookView value)
    {
        RefreshWizard();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        RefreshWizard();
    }

    partial void OnIsMoreOptionsOpenChanged(bool value)
    {
        RefreshWizard();
    }

    /// <summary>Only the print properties the wizard shows trigger a refresh, and only of the parts they feed.</summary>
    private void OnPrintChanged(string? property)
    {
        if (property == nameof(Printing.PrintViewModel.PageNumbers))
        {
            OnPropertyChanged(nameof(ShowsPageNumbers));
        }

        if (_refreshing || property is null || !PrintPropertiesShown.Contains(property))
        {
            return;
        }

        RefreshPrintParts();
    }

    private static readonly HashSet<string> PrintPropertiesShown =
    [
        nameof(Printing.PrintViewModel.SelectedPrinter),
        nameof(Printing.PrintViewModel.SelectedPaper),
        nameof(Printing.PrintViewModel.IsManual),
        nameof(Printing.PrintViewModel.IsBusy),
        nameof(Printing.PrintViewModel.PrintStatus),
        nameof(Printing.PrintViewModel.PrintStatusKind),
        nameof(Printing.PrintViewModel.TestSheetSent),
    ];

    private bool _refreshing;

    /// <summary>Recomputes what depends on the print options and status; the cover, stack and fold steps are left alone.</summary>
    private void RefreshPrintParts()
    {
        var sheets = SheetCount;
        RailSteps = BuildRail(sheets);
        (Heading, Subheading) = HeadingFor(sheets);
        BuildFooter();
        TestSheetText = BuildTestSheetText();
        StackTitle = $"{Plural(sheets, "sheet")} · {Plural(PageCount, "page")} · {Print.SelectedPaper}";
        NotifyCommands();
    }

    [RelayCommand(CanExecute = nameof(CanGoToStep))]
    private void GoToStep(int index)
    {
        Step = (WizardStep)index;
    }

    private bool CanGoToStep(int index)
    {
        return !IsBusy && (index == 0 || IsLoaded);
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        Step = (WizardStep)((int)Step - 1);
    }

    private bool CanGoBack()
    {
        return !IsBusy && Step is WizardStep.Check or WizardStep.Print;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        switch (Step)
        {
            case WizardStep.Open:
                Step = WizardStep.Check;
                break;
            case WizardStep.Check:
                Step = WizardStep.Print;
                break;
            case WizardStep.Print:
                await Print.PrintCommand.ExecuteAsync(null);
                break;
        }
    }

    private bool CanGoNext()
    {
        return Step switch
        {
            WizardStep.Open or WizardStep.Check => IsLoaded && !IsBusy,
            WizardStep.Print => Print.PrintCommand.CanExecute(null),
            _ => false,
        };
    }

    [RelayCommand]
    private void PrintAnother()
    {
        Print.ClearStatus();
        Step = WizardStep.Print;
    }

    [RelayCommand]
    private void MakeAnother()
    {
        StartOver();
    }

    [RelayCommand]
    private void ShowReadingView()
    {
        View = BookView.Reading;
    }

    /// <summary>Recomputes every derived string and list of the wizard from the current state.</summary>
    private void RefreshWizard()
    {
        var sheets = SheetCount;
        var pages = PageCount;
        FolderName = IsLoaded ? Path.GetFileName(Path.TrimEndingDirectorySeparator(FolderPath)) : string.Empty;
        TitleSuffix = FolderName.Length > 0 ? TitleSuffixPrefix + FolderName : string.Empty;
        CoverImage = IsLoaded ? Sheets.FirstOrDefault()?.Front.Right.Image : null;
        Summary = IsLoaded ? BuildSummary(pages, sheets) : string.Empty;
        RailSteps = BuildRail(sheets);
        (Heading, Subheading) = HeadingFor(sheets);
        BuildFooter();
        TestSheetText = BuildTestSheetText();
        StackTitle = $"{Plural(sheets, "sheet")} · {Plural(pages, "page")} · {Print.SelectedPaper}";
        StackLayers = BuildStackLayers(sheets);
        StackLeft = Sheets.FirstOrDefault()?.Front.Left.Image;
        StackRight = Sheets.FirstOrDefault()?.Front.Right.Image;
        FoldSteps = BuildFoldSteps(sheets);
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(IsBusy));
        GoToStepCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    private const string TestIdleText = "Prints only Sheet 1, so you can check the printer flips the paper the right way before you spend the rest.";

    private const string TestSentText = "Test sheet sent. Flip it over: page 1 should sit behind the cover, the right way up.";
    private const string UpsideDownHidden = " Upside down? Open More options to change how the backs are turned.";
    private const string UpsideDownAutomatic = " Upside down? Change Flip edge under More options.";
    private const string UpsideDownManual = " Upside down? Turn Rotate backs 180° on or off under More options.";

    private string BuildTestSheetText()
    {
        if (!Print.TestSheetSent)
        {
            return TestIdleText;
        }

        if (!IsMoreOptionsOpen)
        {
            return TestSentText + UpsideDownHidden;
        }

        return TestSentText + (Print.IsManual ? UpsideDownManual : UpsideDownAutomatic);
    }

    private IReadOnlyList<RailStepViewModel> BuildRail(int sheets)
    {
        var finished = Step == WizardStep.Done;
        var current = Math.Min((int)Step, RailStepCount - 1);
        var summaries = new[] { IsLoaded ? FolderName : NotLoadedFolderSummary, CheckSummary(sheets), finished ? SentSummary : PrintSummary() };
        return Enumerable.Range(0, RailStepCount)
            .Select(i => new RailStepViewModel(i, i + 1, RailTitles[i], summaries[i], StateOf(i, current, finished), CanGoToStep(i), TooltipFor(i, finished)))
            .ToList();
    }

    /// <summary>The rail shows two words of the printer name; the Print row carries the full name on hover.</summary>
    private string? TooltipFor(int index, bool finished)
    {
        return index == PrintRailIndex && !finished && Print.SelectedPrinter is { } printer ? $"{printer.Name} · {Print.SelectedPaper}" : null;
    }

    private static StepState StateOf(int index, int current, bool finished)
    {
        if (finished || index < current)
        {
            return StepState.Done;
        }

        return index == current ? StepState.Current : StepState.Todo;
    }

    private string CheckSummary(int sheets)
    {
        if (!IsLoaded)
        {
            return NotLoadedCheckSummary;
        }

        return Plural(sheets, "sheet") + (Chips.Count > 0 ? $" · {Chips.Count} to check" : string.Empty);
    }

    private string PrintSummary()
    {
        var printer = Print.SelectedPrinter?.Name;
        var name = printer is null ? NoPrinterName : string.Join(' ', printer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2));
        return $"{name} · {Print.SelectedPaper}";
    }

    private (string Heading, string Subheading) HeadingFor(int sheets)
    {
        return Step switch
        {
            WizardStep.Open when IsLoaded => (HeadingOpenLoaded, SubheadingOpenLoaded),
            WizardStep.Open => (HeadingOpen, SubheadingOpen),
            WizardStep.Check => (HeadingCheck, CheckSubheading()),
            WizardStep.Print => (HeadingPrint, $"{PrintLead()} {Plural(sheets, "sheet")} of {Print.SelectedPaper}."),
            _ => (HeadingDone, SubheadingDone),
        };
    }

    private string CheckSubheading()
    {
        return IsReadingView ? SubheadingReading : SubheadingSheets;
    }

    private string PrintLead()
    {
        return Print.IsManual ? LeadManual : LeadAutomatic;
    }

    private void BuildFooter()
    {
        var showPrintStatus = Step >= WizardStep.Print && !string.IsNullOrEmpty(Print.PrintStatus);
        FooterText = showPrintStatus ? Print.PrintStatus! : Status;
        FooterTone = FooterToneFor(showPrintStatus);
        HasBack = Step is WizardStep.Check or WizardStep.Print;
        HasNext = Step != WizardStep.Done;
        NextIsPrint = Step == WizardStep.Print;
        NextLabel = Step switch
        {
            WizardStep.Open => NextOpenLabel,
            WizardStep.Check => NextCheckLabel,
            _ => NextPrintLabel,
        };
    }

    private FooterTone FooterToneFor(bool showPrintStatus)
    {
        if (!showPrintStatus)
        {
            return FooterTone.Normal;
        }

        return Print.PrintStatusKind == Printing.PrintStatusKind.Failure ? FooterTone.Warning : FooterTone.Accent;
    }

    private static IReadOnlyList<StackLayerViewModel> BuildStackLayers(int sheets)
    {
        var behind = Math.Max(0, Math.Min(sheets, MaxStackLayers) - 1);
        return Enumerable.Range(0, behind)
            .Select(i => new StackLayerViewModel((behind - i) * StackLayerX, (behind - i) * StackLayerY))
            .ToList();
    }

    private static IReadOnlyList<FoldStepViewModel> BuildFoldSteps(int sheets)
    {
        return
        [
            new("1", "Grab the stack", $"Take all {sheets} sheets off the tray. Keep them in the order they came out."),
            new("2", "Fold it in half", "Fold the whole stack at once, cover on the outside. Run a thumbnail along the crease."),
            new("3", "Staple the spine", "Two staples on the fold. That's a book. Go read it to someone."),
        ];
    }

    private static string Plural(int count, string noun)
    {
        return $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
    }

    private static IReadOnlyList<string> BuildChips(IReadOnlyList<BookFinding> findings, int sheetCount, IReadOnlyList<string> failedImages)
    {
        var chips = findings.Select(finding => Describe(finding, sheetCount)).ToList();
        if (failedImages.Count > 0)
        {
            chips.Add($"{failedImages.Count} {(failedImages.Count == 1 ? "file" : "files")} could not be read as {(failedImages.Count == 1 ? "an image" : "images")}");
        }

        return chips;
    }

    private static string Describe(BookFinding finding, int sheetCount)
    {
        return finding.Issue switch
        {
            BookIssue.TooThick => $"{sheetCount} sheets is too thick to fold flat. {Imposition.MaxFlatSheets * Imposition.PagesPerSheet} pages is about the limit.",
            BookIssue.MissingCover => "No Cover file, so the first page is the front cover",
            BookIssue.MissingBackCover => "No Back cover file",
            BookIssue.UnrecognisedFiles => finding.Files.Count == 1 ? "1 file has a name I don't recognise" : $"{finding.Files.Count} files have names I don't recognise",
            BookIssue.DuplicateCover => "More than one Cover file",
            BookIssue.DuplicateBackCover => "More than one Back cover file",
            _ => throw new ArgumentOutOfRangeException(nameof(finding), finding.Issue, null),
        };
    }
}
