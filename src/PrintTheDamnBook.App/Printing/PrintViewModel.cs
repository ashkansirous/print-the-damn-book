using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Printing;

/// <summary>Printer, paper and print commands for the main window. Fed the current sheets via <see cref="SetSheets"/>.</summary>
public sealed partial class PrintViewModel : ObservableObject, IManualDuplexSession
{
    private const string CoverPrompt = "The cover sheet has been sent. Load the cover paper, then press OK to print the rest.";

    private const string NoDuplexReason = " does not report two-sided printing, so manual two-sided printing is selected.";
    private const string ProbeFailedReason = "Couldn't check two-sided support for ";
    private const string ProbeFailedSuffix = ", so manual two-sided printing is selected.";

    private const string FallbackJobName = "Booklet";
    private const string CoverSent = "The cover sheet was sent.";
    private const string CoverSentRestNotPrinted = "The cover sheet was sent, but the rest of the booklet was not printed.";
    private const string CoverSentRestMayBeIncomplete = "The cover sheet was sent, but the rest of the booklet may be incomplete.";
    private const string RestCancelled = " You chose not to continue.";

    private readonly PrinterService _printer;
    private readonly IPrintPrompts _prompts;
    private readonly ManualDuplexRunner _manual;
    private IReadOnlyList<Sheet> _sheets = [];
    private string _bookName = FallbackJobName;
    private bool _forgetRunsWhenIdle;

    public PrintViewModel(PrinterService printer, IPrintPrompts prompts)
    {
        _printer = printer;
        _prompts = prompts;
        _manual = new ManualDuplexRunner(printer, prompts, this);
    }

    public IReadOnlyList<PaperKind> PaperSizes { get; } = Enum.GetValues<PaperKind>();

    [ObservableProperty]
    public partial IReadOnlyList<PrinterChoice> Printers { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintTestSheetCommand))]
    public partial PrinterChoice? SelectedPrinter { get; set; }

    [ObservableProperty]
    public partial PaperKind SelectedPaper { get; set; } = PaperKind.A4;

    [ObservableProperty]
    public partial bool SeparateCoverJob { get; set; }

    public IReadOnlyList<DuplexChoice> DuplexChoices { get; } = DuplexChoice.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManual))]
    public partial DuplexChoice SelectedDuplex { get; set; } = DuplexChoice.All[0];

    /// <summary>Why Manual was selected for the chosen printer; null when the user picked it or the printer does duplex.</summary>
    [ObservableProperty]
    public partial string? DuplexNote { get; set; }

    /// <summary>Print the backs from the last sheet to the first (manual mode, session only).</summary>
    [ObservableProperty]
    public partial bool ReverseBacks { get; set; }

    /// <summary>Turn every back 180 degrees (manual mode, session only). The default matches the reload instruction.</summary>
    [ObservableProperty]
    public partial bool RotateBacks { get; set; } = ManualDuplexPlan.DefaultRotateBacks;

    public bool IsManual => SelectedDuplex.Mode == DuplexMode.Manual;

    [ObservableProperty]
    public partial string? PrintStatus { get; set; }

    /// <summary>True while a job is being rendered and sent; the print buttons are disabled meanwhile.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintTestSheetCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintBacksAgainCommand))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    /// <summary>The opposite of <see cref="IsBusy"/>; the printer and option controls are enabled only then.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Loads the installed printers off the UI thread; a failure is shown in <see cref="PrintStatus"/>.</summary>
    public async Task LoadPrintersAsync()
    {
        try
        {
            Printers = await _printer.ListPrintersAsync();
            SelectedPrinter = Printers.FirstOrDefault(p => p.IsDefault) ?? Printers.FirstOrDefault();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            PrintStatus = $"Could not list printers: {ex.Message}";
        }
    }

    /// <summary>Sets the sheets to print; <paramref name="bookName"/> (the folder name) names the print jobs.</summary>
    public void SetSheets(IReadOnlyList<Sheet> sheets, string bookName = "")
    {
        _sheets = sheets;
        _bookName = string.IsNullOrWhiteSpace(bookName) ? FallbackJobName : bookName;
        ClearStatusUnlessBusy();
        ForgetLastRun();
        PrintCommand.NotifyCanExecuteChanged();
        PrintTestSheetCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPrinterChanged(PrinterChoice? value)
    {
        ClearStatusUnlessBusy();
        ForgetLastRun();
        DuplexNote = null;
        _ = ApplyPrinterDuplexAsync(value);
    }

    /// <summary>
    /// Selects Manual, with the reason, when the printer cannot (or cannot be checked to) do short-edge duplex. Never
    /// switches Manual back to Automatic, so a user's Manual choice sticks.
    /// </summary>
    private async Task ApplyPrinterDuplexAsync(PrinterChoice? printer)
    {
        if (printer is null)
        {
            return;
        }

        var support = await ProbeDuplexAsync(printer.Name);
        if (support == true || !ReferenceEquals(SelectedPrinter, printer))
        {
            return;
        }

        SelectedDuplex = DuplexChoices.First(c => c.Mode == DuplexMode.Manual);
        DuplexNote = support == false ? printer.Name + NoDuplexReason : ProbeFailedReason + printer.Name + ProbeFailedSuffix;
    }

    private async Task<bool?> ProbeDuplexAsync(string printerName)
    {
        try
        {
            return await _printer.ProbeShortEdgeDuplexAsync(printerName);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    partial void OnSelectedDuplexChanged(DuplexChoice value)
    {
        DuplexNote = null;
    }

    partial void OnSelectedPaperChanged(PaperKind value)
    {
        ClearStatusUnlessBusy();
        ForgetLastRun();
    }

    private void ClearStatusUnlessBusy()
    {
        if (!IsBusy)
        {
            PrintStatus = null;
        }
    }

    /// <summary>Forgets the remembered manual runs; while a job is running this waits until it has finished.</summary>
    private void ForgetLastRun()
    {
        if (IsBusy)
        {
            _forgetRunsWhenIdle = true;
            return;
        }

        _manual.Forget();
        PrintBacksAgainCommand.NotifyCanExecuteChanged();
    }

    private async Task RunBusyAsync(Func<Task> work)
    {
        IsBusy = true;
        try
        {
            await work();
        }
        finally
        {
            IsBusy = false;
            ApplyPendingForget();
        }
    }

    private void ApplyPendingForget()
    {
        if (_forgetRunsWhenIdle)
        {
            _forgetRunsWhenIdle = false;
            ForgetLastRun();
        }
    }

    private bool CanPrint()
    {
        return !IsBusy && _sheets.Count > 0 && SelectedPrinter is not null;
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private Task PrintAsync()
    {
        return RunBusyAsync(() => PrintBookletAsync(_sheets, CurrentTarget()));
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private Task PrintTestSheetAsync()
    {
        var sheet = ManualDuplexPlan.PickTestSheet(_sheets)!;
        return RunBusyAsync(() => SendAsync(CurrentTarget(), [sheet], PrintPart.TestSheet));
    }

    private bool CanPrintBacksAgain()
    {
        return !IsBusy && _manual.HasBacksToRepeat;
    }

    [RelayCommand(CanExecute = nameof(CanPrintBacksAgain))]
    private Task PrintBacksAgainAsync()
    {
        return RunBusyAsync(_manual.PrintBacksAgainAsync);
    }

    private PrintTarget CurrentTarget()
    {
        return new PrintTarget(SelectedPrinter!.Name, SelectedPaper, _bookName);
    }

    private async Task PrintBookletAsync(IReadOnlyList<Sheet> sheets, PrintTarget target)
    {
        _manual.Forget();
        if (!SeparateCoverJob || sheets.Count < 2)
        {
            await SendAsync(target, sheets, PrintPart.Booklet);
            return;
        }

        if (await SendAsync(target, sheets.Take(1).ToList(), PrintPart.Cover) != PrintOutcome.Sent)
        {
            return;
        }

        if (!_prompts.ConfirmContinue(CoverPrompt))
        {
            PrintStatus = CoverSentRestNotPrinted + RestCancelled;
            return;
        }

        var outcome = await SendAsync(target, sheets.Skip(1).ToList(), PrintPart.Rest);
        if (outcome != PrintOutcome.Sent)
        {
            PrintStatus = CoverPrefix(outcome) + " " + PrintStatus;
        }
    }

    private static string CoverPrefix(PrintOutcome restOutcome)
    {
        return restOutcome switch
        {
            PrintOutcome.NothingSent => CoverSentRestNotPrinted,
            PrintOutcome.MayBePartial => CoverSentRestMayBeIncomplete,
            _ => CoverSent,
        };
    }

    private async Task<PrintOutcome> SendAsync(PrintTarget target, IReadOnlyList<Sheet> sheets, PrintPart part)
    {
        var text = PrintParts.Text(part);
        try
        {
            PrintStatus = $"Sending {text.Phrase} to {target.PrinterName}...";
            if (IsManual)
            {
                return await _manual.RunAsync(target, sheets, part);
            }

            await _printer.PrintAsync(target.PrinterName, target.Paper, sheets, target.BookName + text.JobSuffix);
            PrintStatus = $"Sent {text.Phrase} to {target.PrinterName}.";
            return PrintOutcome.Sent;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            PrintStatus = $"Could not send {text.Phrase} to {target.PrinterName}.";
            _prompts.ShowError(PrintErrors.JobFailure(ex, target.PrinterName));
            return PrintErrors.OutcomeOf(ex);
        }
    }
}
