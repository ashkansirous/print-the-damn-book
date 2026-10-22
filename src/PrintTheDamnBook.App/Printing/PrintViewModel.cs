using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintTheDamnBook.App.Settings;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Printing;

/// <summary>Printer, paper and print commands for the main window. Fed the current sheets via <see cref="SetSheets"/>.</summary>
public sealed partial class PrintViewModel : ObservableObject, IManualDuplexSession
{
    private const string CoverPrompt = "The cover sheet has been sent. Load the paper for the inside pages, then press OK to print the rest.";

    private const string NoDuplexReason = " does not report two-sided printing on the ";
    private const string NoDuplexSuffix = ", so manual two-sided printing is selected.";
    private const string ShortEdgeName = "short edge";
    private const string LongEdgeName = "long edge";
    private const string ProbeFailedReason = "Couldn't check two-sided support for ";
    private const string ProbeFailedSuffix = ", so manual two-sided printing is selected.";

    private const string SettingsNotSaved = "Couldn't save your settings.";
    private const double PreviewBoxWidth = 150;
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

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
    private readonly SettingsStore _store;
    private volatile PrintSettings _settings;
    private bool _forgetRunsWhenIdle;

    /// <summary>True while values are being set from the settings file or a forced switch, so they are not saved back.</summary>
    private bool _applying;

    /// <summary>True while the printer list is being filled, so a fallback selection is not saved as the user's choice.</summary>
    private bool _loadingPrinters;

    /// <summary>The Two-sided mode the user chose for the selected printer; differs from the shown one while Manual is forced.</summary>
    private DuplexMode _chosenDuplex = DuplexMode.Automatic;

    private bool _manualForced;
    private CancellationTokenSource? _saveTimer;
    private volatile bool _savePending;
    private readonly object _saveLock = new();

    public PrintViewModel(PrinterService printer, IPrintPrompts prompts, SettingsStore store)
    {
        _printer = printer;
        _prompts = prompts;
        _store = store;
        _manual = new ManualDuplexRunner(printer, prompts, this);
        _settings = store.Load();
        ApplyGlobalSettings(_settings);
    }

    public IReadOnlyList<PaperKind> PaperSizes { get; } = Enum.GetValues<PaperKind>();

    public IReadOnlyList<LayoutChoice<ScalingMode>> ScalingChoices => LayoutChoices.Scaling;

    public IReadOnlyList<LayoutChoice<FlipEdge>> FlipChoices => LayoutChoices.Flip;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageStretch))]
    public partial ScalingMode Scaling { get; set; } = ScalingMode.Fit;

    /// <summary>How the sheet preview draws a page: Fill shows the crop.</summary>
    public Stretch ImageStretch => Scaling == ScalingMode.Fill ? Stretch.UniformToFill : Stretch.Uniform;

    /// <summary>Applies to Automatic only; in Manual, <see cref="RotateBacks"/> already covers it.</summary>
    [ObservableProperty]
    public partial FlipEdge Flip { get; set; } = FlipEdge.ShortEdge;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    [NotifyPropertyChangedFor(nameof(PreviewInset))]
    public partial double MarginMm { get; set; } = HalfSheetLayout.DefaultMarginMm;

    /// <summary>What the margin box shows; typed text is parsed on commit and replaced by the clamped value.</summary>
    [ObservableProperty]
    public partial string MarginText { get; set; } = HalfSheetLayout.FormatMargin(HalfSheetLayout.DefaultMarginMm, CultureInfo.CurrentCulture);

    /// <summary>The preview box of one page: the real half-sheet aspect with the margin inset.</summary>
    public PreviewBox Preview => HalfSheetLayout.Preview(Paper.LandscapeSheetMm(SelectedPaper), MarginMm, PreviewBoxWidth);

    public Thickness PreviewInset => new(Preview.Inset);

    [ObservableProperty]
    public partial IReadOnlyList<PrinterChoice> Printers { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintTestSheetCommand))]
    public partial PrinterChoice? SelectedPrinter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    [NotifyPropertyChangedFor(nameof(PreviewInset))]
    public partial PaperKind SelectedPaper { get; set; } = PaperKind.A4;

    [ObservableProperty]
    public partial bool SeparateCoverJob { get; set; }

    /// <summary>Set while the last attempt to save the settings failed; separate from <see cref="PrintStatus"/>.</summary>
    [ObservableProperty]
    public partial string? SettingsStatus { get; set; }

    public IReadOnlyList<DuplexChoice> DuplexChoices { get; } = DuplexChoice.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManual))]
    [NotifyPropertyChangedFor(nameof(IsAutomatic))]
    [NotifyPropertyChangedFor(nameof(DuplexMode))]
    public partial DuplexChoice SelectedDuplex { get; set; } = DuplexChoice.All[0];

    /// <summary>Why Manual was selected for the chosen printer; null when the user picked it or the printer does duplex.</summary>
    [ObservableProperty]
    public partial string? DuplexNote { get; set; }

    /// <summary>Print the backs from the last sheet to the first (manual mode, remembered per printer).</summary>
    [ObservableProperty]
    public partial bool ReverseBacks { get; set; }

    /// <summary>Turn every back 180 degrees (manual mode, remembered per printer). The default matches the reload instruction.</summary>
    [ObservableProperty]
    public partial bool RotateBacks { get; set; } = ManualDuplexPlan.DefaultRotateBacks;

    /// <summary>The selected Two-sided mode, for the segmented control.</summary>
    public DuplexMode DuplexMode
    {
        get => SelectedDuplex.Mode;
        set => SelectedDuplex = DuplexChoices.First(c => c.Mode == value);
    }

    public bool IsManual => SelectedDuplex.Mode == DuplexMode.Manual;

    public bool IsAutomatic => !IsManual;

    [ObservableProperty]
    public partial string? PrintStatus { get; set; }

    /// <summary>Whether <see cref="PrintStatus"/> reports a failure; set together with the text by <see cref="SetStatus"/>.</summary>
    [ObservableProperty]
    public partial PrintStatusKind PrintStatusKind { get; set; }

    public void SetStatus(string text, PrintStatusKind kind)
    {
        PrintStatusKind = kind;
        PrintStatus = text;
    }

    /// <summary>Clears the status text and its kind together.</summary>
    public void ClearStatus()
    {
        PrintStatusKind = PrintStatusKind.Info;
        PrintStatus = null;
    }

    /// <summary>True after the test sheet was sent for the current printer and paper; changes the test-sheet text.</summary>
    [ObservableProperty]
    public partial bool TestSheetSent { get; set; }

    /// <summary>Raised when the whole booklet has been sent to the printer (not for a test sheet or a partial send).</summary>
    public event EventHandler? BookletSent;

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
            _loadingPrinters = true;
            SelectedPrinter = Printers.FirstOrDefault(p => string.Equals(p.Name, _settings.Printer, StringComparison.OrdinalIgnoreCase))
                ?? Printers.FirstOrDefault(p => p.IsDefault)
                ?? Printers.FirstOrDefault();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SetStatus($"Could not list printers: {ex.Message}", PrintStatusKind.Failure);
        }
        finally
        {
            _loadingPrinters = false;
        }
    }

    /// <summary>Sets the sheets to print; <paramref name="bookName"/> (the folder name) names the print jobs.</summary>
    public void SetSheets(IReadOnlyList<Sheet> sheets, string bookName = "")
    {
        _sheets = sheets;
        _bookName = string.IsNullOrWhiteSpace(bookName) ? FallbackJobName : bookName;
        TestSheetSent = false;
        ClearStatusUnlessBusy();
        ForgetLastRun();
        PrintCommand.NotifyCanExecuteChanged();
        PrintTestSheetCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPrinterChanged(PrinterChoice? value)
    {
        TestSheetSent = false;
        ClearStatusUnlessBusy();
        ForgetLastRun();
        ApplyPrinterSettings(value);
        if (!_loadingPrinters && value is not null)
        {
            _settings = _settings with { Printer = value.Name };
            Persist();
        }

        DuplexNote = null;
        _ = ApplyPrinterDuplexAsync(value);
    }

    partial void OnFlipChanged(FlipEdge value)
    {
        TestSheetSent = false;
        Persist();
        if (!_manualForced)
        {
            DuplexNote = null;
        }

        _ = ApplyPrinterDuplexAsync(SelectedPrinter);
    }

    partial void OnScalingChanged(ScalingMode value)
    {
        LayoutChanged();
    }

    partial void OnMarginMmChanged(double value)
    {
        var margin = HalfSheetLayout.NormalizeMargin(value);
        if (margin != value)
        {
            MarginMm = margin;
            return;
        }

        MarginText = HalfSheetLayout.FormatMargin(margin, CultureInfo.CurrentCulture);
        LayoutChanged();
    }

    partial void OnMarginTextChanged(string value)
    {
        if (HalfSheetLayout.TryParseMargin(value, out var parsed))
        {
            MarginMm = HalfSheetLayout.NormalizeMargin(parsed);
        }

        MarginText = HalfSheetLayout.FormatMargin(MarginMm, CultureInfo.CurrentCulture);
    }

    /// <summary>Scaling and margin change what the fronts look like, so a remembered first pass no longer matches.</summary>
    private void LayoutChanged()
    {
        Persist();
        ClearStatusUnlessBusy();
        ForgetLastRun();
    }

    partial void OnSeparateCoverJobChanged(bool value)
    {
        Persist();
    }

    partial void OnReverseBacksChanged(bool value)
    {
        TestSheetSent = false;
        Persist();
    }

    partial void OnRotateBacksChanged(bool value)
    {
        TestSheetSent = false;
        Persist();
    }

    private void ApplyGlobalSettings(PrintSettings settings)
    {
        _applying = true;
        try
        {
            Scaling = settings.Scaling;
            Flip = settings.Flip;
            MarginMm = settings.MarginMm;
            SelectedPaper = settings.Paper;
            SeparateCoverJob = settings.SeparateCoverJob;
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>Loads what is remembered for the printer (defaults for a new one) without saving it back.</summary>
    private void ApplyPrinterSettings(PrinterChoice? printer)
    {
        if (printer is null)
        {
            return;
        }

        var remembered = _settings.ForPrinter(printer.Name);
        _applying = true;
        try
        {
            _chosenDuplex = remembered.Duplex;
            _manualForced = false;
            SelectedDuplex = DuplexChoices.First(c => c.Mode == remembered.Duplex);
            ReverseBacks = remembered.ReverseBacks;
            RotateBacks = remembered.RotateBacks;
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>Updates the in-memory settings and schedules a save. The remembered printer changes only when the user picks one.</summary>
    private void Persist()
    {
        if (_applying)
        {
            return;
        }

        _settings = _settings with { Scaling = Scaling, Flip = Flip, MarginMm = MarginMm, Paper = SelectedPaper, SeparateCoverJob = SeparateCoverJob };
        if (SelectedPrinter is not null)
        {
            _settings = _settings.WithPrinter(SelectedPrinter.Name, new PrinterSettings(_chosenDuplex, ReverseBacks, RotateBacks));
        }

        ScheduleSave();
    }

    private void ScheduleSave()
    {
        StopSaveTimer();
        _saveTimer = new CancellationTokenSource();
        _savePending = true;
        _ = SaveAfterDelayAsync(_saveTimer.Token);
    }

    private async Task SaveAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SaveDelay, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var saved = await Task.Run(SaveLatest);
        SettingsStatus = saved ? null : SettingsNotSaved;
    }

    private void StopSaveTimer()
    {
        _saveTimer?.Cancel();
        _saveTimer?.Dispose();
        _saveTimer = null;
    }

    /// <summary>
    /// Writes the newest settings if a save is pending. Saves are serialised, so an older snapshot can never land after
    /// a newer one; the change stays pending until a write succeeds or a newer change replaces it.
    /// </summary>
    private bool SaveLatest()
    {
        lock (_saveLock)
        {
            if (!_savePending)
            {
                return true;
            }

            var snapshot = _settings;
            if (!_store.Save(snapshot))
            {
                return false;
            }

            if (ReferenceEquals(_settings, snapshot))
            {
                _savePending = false;
            }

            return true;
        }
    }

    /// <summary>Writes any change still waiting for the save delay; call when the window closes. Never throws.</summary>
    public void FlushSettings()
    {
        try
        {
            StopSaveTimer();
            if (!SaveLatest())
            {
                SettingsStatus = SettingsNotSaved;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The window is closing: nothing useful is left to do with a failed save.
        }
    }

    /// <summary>
    /// Selects Manual, with the reason, when the printer cannot (or cannot be checked to) do duplex on the chosen flip
    /// edge. The user's own choice is kept apart, so the forced Manual is never saved and is undone when a later check
    /// passes. Nothing is probed while the user has chosen Manual.
    /// </summary>
    private async Task ApplyPrinterDuplexAsync(PrinterChoice? printer)
    {
        if (printer is null || (IsManual && !_manualForced))
        {
            return;
        }

        var flip = Flip;
        var support = await ProbeDuplexAsync(printer.Name, flip);
        if (!ReferenceEquals(SelectedPrinter, printer) || Flip != flip || (IsManual && !_manualForced))
        {
            return;
        }

        if (support == true)
        {
            RestoreChosenDuplex();
            return;
        }

        SetDuplexWithoutSaving(DuplexMode.Manual);
        _manualForced = true;
        DuplexNote = support == false ? printer.Name + NoDuplexReason + EdgeName(flip) + NoDuplexSuffix : ProbeFailedReason + printer.Name + ProbeFailedSuffix;
    }

    private void RestoreChosenDuplex()
    {
        if (_manualForced)
        {
            _manualForced = false;
            SetDuplexWithoutSaving(_chosenDuplex);
            DuplexNote = null;
        }
    }

    private static string EdgeName(FlipEdge flip)
    {
        return flip == FlipEdge.LongEdge ? LongEdgeName : ShortEdgeName;
    }

    private void SetDuplexWithoutSaving(DuplexMode mode)
    {
        _applying = true;
        try
        {
            SelectedDuplex = DuplexChoices.First(c => c.Mode == mode);
        }
        finally
        {
            _applying = false;
        }
    }

    private async Task<bool?> ProbeDuplexAsync(string printerName, FlipEdge flip)
    {
        try
        {
            return await _printer.ProbeDuplexAsync(printerName, flip);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    partial void OnSelectedDuplexChanged(DuplexChoice value)
    {
        if (_applying)
        {
            return;
        }

        TestSheetSent = false;
        _manualForced = false;
        _chosenDuplex = value.Mode;
        DuplexNote = null;
        Persist();
        if (value.Mode == DuplexMode.Automatic)
        {
            _ = ApplyPrinterDuplexAsync(SelectedPrinter);
        }
    }

    partial void OnSelectedPaperChanged(PaperKind value)
    {
        TestSheetSent = false;
        Persist();
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
        return RunBusyAsync(SendTestSheetAsync);
    }

    private async Task SendTestSheetAsync()
    {
        var sheet = ManualDuplexPlan.PickTestSheet(_sheets)!;
        var rotateBefore = RotateBacks;
        var outcome = await SendAsync(CurrentTarget(), [sheet], PrintPart.TestSheet);
        // Answering "upside down" flips RotateBacks during the send; that sheet no longer matches the options.
        TestSheetSent = outcome == PrintOutcome.Sent && RotateBacks == rotateBefore;
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
        return new PrintTarget(SelectedPrinter!.Name, SelectedPaper, new JobLayout(Scaling, MarginMm, Flip), _bookName);
    }

    private async Task PrintBookletAsync(IReadOnlyList<Sheet> sheets, PrintTarget target)
    {
        _manual.Forget();
        if (!SeparateCoverJob || sheets.Count < 2)
        {
            NotifyIfSent(await SendAsync(target, sheets, PrintPart.Booklet));
            return;
        }

        if (await SendAsync(target, sheets.Take(1).ToList(), PrintPart.Cover) != PrintOutcome.Sent)
        {
            return;
        }

        if (!_prompts.ConfirmContinue(CoverPrompt))
        {
            SetStatus(CoverSentRestNotPrinted + RestCancelled, PrintStatusKind.Failure);
            return;
        }

        var outcome = await SendAsync(target, sheets.Skip(1).ToList(), PrintPart.Rest);
        if (outcome != PrintOutcome.Sent)
        {
            SetStatus(CoverPrefix(outcome) + " " + PrintStatus, PrintStatusKind.Failure);
        }

        NotifyIfSent(outcome);
    }

    private void NotifyIfSent(PrintOutcome outcome)
    {
        if (outcome == PrintOutcome.Sent)
        {
            BookletSent?.Invoke(this, EventArgs.Empty);
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
            SetStatus($"Sending {text.Phrase} to {target.PrinterName}…", PrintStatusKind.Info);
            if (IsManual)
            {
                return await _manual.RunAsync(target, sheets, part);
            }

            await _printer.PrintAsync(target.PrinterName, target.Paper, target.Layout, sheets, target.BookName + text.JobSuffix);
            SetStatus($"Sent {text.Phrase} to {target.PrinterName}.", PrintStatusKind.Info);
            return PrintOutcome.Sent;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SetStatus($"Could not send {text.Phrase} to {target.PrinterName}.", PrintStatusKind.Failure);
            _prompts.ShowError(PrintErrors.JobFailure(ex, target.PrinterName));
            return PrintErrors.OutcomeOf(ex);
        }
    }
}
