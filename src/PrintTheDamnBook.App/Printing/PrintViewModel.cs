using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Printing;

/// <summary>Printer, paper and print commands for the main window. Fed the current sheets via <see cref="SetSheets"/>.</summary>
public sealed partial class PrintViewModel : ObservableObject
{
    private const string CoverPrompt = "The cover sheet has been sent. Load the cover paper, then press OK to print the rest.";

    private const string FallbackJobName = "Booklet";
    private const string CoverSentRestNotPrinted = "The cover sheet was sent, but the rest of the booklet was not printed.";
    private const string RestCancelled = " You chose not to continue.";

    private static readonly IReadOnlyDictionary<PrintPart, PartText> PartTexts = new Dictionary<PrintPart, PartText>
    {
        [PrintPart.Booklet] = new("the booklet", string.Empty),
        [PrintPart.Cover] = new("the cover sheet", " - cover sheet"),
        [PrintPart.Rest] = new("the rest of the booklet", " - inside pages"),
        [PrintPart.TestSheet] = new("the test sheet", " - test sheet"),
    };

    private readonly PrinterService _printer;
    private readonly IPrintPrompts _prompts;
    private IReadOnlyList<Sheet> _sheets = [];
    private string _bookName = FallbackJobName;

    public PrintViewModel(PrinterService printer, IPrintPrompts prompts)
    {
        _printer = printer;
        _prompts = prompts;
    }

    private enum PrintPart
    {
        Booklet,
        Cover,
        Rest,
        TestSheet,
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

    [ObservableProperty]
    public partial string? PrintStatus { get; set; }

    /// <summary>True while a job is being rendered and sent; the print buttons are disabled meanwhile.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintTestSheetCommand))]
    public partial bool IsBusy { get; set; }

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
        PrintCommand.NotifyCanExecuteChanged();
        PrintTestSheetCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPrinterChanged(PrinterChoice? value)
    {
        ClearStatusUnlessBusy();
    }

    partial void OnSelectedPaperChanged(PaperKind value)
    {
        ClearStatusUnlessBusy();
    }

    private void ClearStatusUnlessBusy()
    {
        if (!IsBusy)
        {
            PrintStatus = null;
        }
    }

    private bool CanPrint()
    {
        return !IsBusy && _sheets.Count > 0 && SelectedPrinter is not null;
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private async Task PrintAsync()
    {
        IsBusy = true;
        try
        {
            await PrintBookletAsync(_sheets, new PrintTarget(SelectedPrinter!.Name, SelectedPaper, _bookName));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private async Task PrintTestSheetAsync()
    {
        IsBusy = true;
        try
        {
            await SendAsync(new PrintTarget(SelectedPrinter!.Name, SelectedPaper, _bookName), _sheets.Take(1).ToList(), PrintPart.TestSheet);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PrintBookletAsync(IReadOnlyList<Sheet> sheets, PrintTarget target)
    {
        if (!SeparateCoverJob || sheets.Count < 2)
        {
            await SendAsync(target, sheets, PrintPart.Booklet);
            return;
        }

        if (!await SendAsync(target, sheets.Take(1).ToList(), PrintPart.Cover))
        {
            return;
        }

        if (!_prompts.ConfirmContinue(CoverPrompt))
        {
            PrintStatus = CoverSentRestNotPrinted + RestCancelled;
        }
        else if (!await SendAsync(target, sheets.Skip(1).ToList(), PrintPart.Rest))
        {
            PrintStatus = $"{CoverSentRestNotPrinted} {PrintStatus}";
        }
    }

    private async Task<bool> SendAsync(PrintTarget target, IReadOnlyList<Sheet> sheets, PrintPart part)
    {
        var text = PartTexts[part];
        try
        {
            PrintStatus = $"Sending {text.Phrase} to {target.PrinterName}...";
            await _printer.PrintAsync(target.PrinterName, target.Paper, sheets, target.BookName + text.JobSuffix);
            PrintStatus = $"Sent {text.Phrase} to {target.PrinterName}.";
            return true;
        }
        catch (PrintFailedException ex)
        {
            PrintStatus = $"Could not send {text.Phrase} to {target.PrinterName}.";
            _prompts.ShowError(ex.Message);
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            PrintStatus = $"Could not send {text.Phrase} to {target.PrinterName}.";
            _prompts.ShowError($"Printing to {target.PrinterName} failed: {ex.Message}");
            return false;
        }
    }

    private sealed record PartText(string Phrase, string JobSuffix);

    private sealed record PrintTarget(string PrinterName, PaperKind Paper, string BookName);
}
