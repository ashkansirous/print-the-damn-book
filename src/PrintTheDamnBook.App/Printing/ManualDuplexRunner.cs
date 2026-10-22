using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Printing;

/// <summary>
/// Manual two-sided printing: every front, then (after the user reloads the stack) every back. Remembers the completed
/// book passes so their backs can be printed again; test sheets are never remembered.
/// </summary>
internal sealed class ManualDuplexRunner(PrinterService printer, IPrintPrompts prompts, IManualDuplexSession session)
{
    public const string PrintBacksAgainLabel = "Print backs again";

    private const string ReloadPromptTestSheet = "The fronts were sent to the printer. Wait until the printer has stopped, then take the stack out of the output tray.\n\nAs the sheet says: \"" + FaceRenderer.FeedEdgeInstruction + "\".\n\nWhen the backs have printed you will be asked whether they came out upside down.";
    private const string ReloadPromptBooklet = "The fronts were sent to the printer. Wait until the printer has stopped, then take the stack out of the output tray and put it back in the paper tray printed side UP, in the same order, with the same edge going into the printer first as for the fronts.\n\nIf the backs come out upside down or in the wrong order, change the options and use \"" + PrintBacksAgainLabel + "\"; the fronts are not printed twice.";
    private const string FrontsSuffix = " - fronts";
    private const string BacksSuffix = " - backs";
    private const string TryBacksAgain = " Try the backs again?";

    private static readonly PrintPart[] RepeatOrder = [PrintPart.Rest, PrintPart.Cover, PrintPart.Booklet];

    /// <summary>Completed first passes of the current sheets, by part. The rest's backs are offered before the cover's.</summary>
    private readonly Dictionary<PrintPart, ManualRun> _runs = [];

    public bool HasBacksToRepeat => PendingRun() is not null;

    public void Forget()
    {
        _runs.Clear();
    }

    /// <summary>Checks every image (fronts and backs), prints every front, then hands over to the backs pass.</summary>
    public async Task<PrintOutcome> RunAsync(PrintTarget target, IReadOnlyList<Sheet> sheets, PrintPart part)
    {
        var text = PrintParts.Text(part);
        var plan = ManualDuplexPlan.Build(sheets, OptionsFor(part));
        await printer.CheckReadableAsync(plan.Fronts.Concat(plan.Backs).ToList());
        await printer.PrintFacesAsync(target.PrinterName, target.Paper, plan.Fronts, target.BookName + text.JobSuffix + FrontsSuffix);
        var run = new ManualRun(target, sheets, part);
        if (part != PrintPart.TestSheet)
        {
            _runs[part] = run;
        }

        return await PrintBacksAsync(run);
    }

    public Task<PrintOutcome> PrintBacksAgainAsync()
    {
        return PrintBacksAsync(PendingRun()!);
    }

    private ManualRun? PendingRun()
    {
        var part = RepeatOrder.FirstOrDefault(_runs.ContainsKey);
        return _runs.GetValueOrDefault(part);
    }

    private ManualDuplexOptions OptionsFor(PrintPart part)
    {
        return new ManualDuplexOptions(session.ReverseBacks, session.RotateBacks, part == PrintPart.TestSheet);
    }

    /// <summary>Asks the user to put the stack back, then prints every back; a failure offers a retry without reprinting the fronts.</summary>
    private async Task<PrintOutcome> PrintBacksAsync(ManualRun run)
    {
        var text = PrintParts.Text(run.Part);
        var printerName = run.Target.PrinterName;
        var prompt = run.Part == PrintPart.TestSheet ? ReloadPromptTestSheet : ReloadPromptBooklet;
        while (true)
        {
            session.PrintStatus = $"Sent the fronts of {text.Phrase} to {printerName}. Waiting for you to put the stack back.";
            if (!prompts.ConfirmPrintBacks(prompt))
            {
                session.PrintStatus = $"The fronts of {text.Phrase} were sent to the printer; the backs were not. \"{PrintBacksAgainLabel}\" prints them.";
                return PrintOutcome.FrontsOnly;
            }

            var error = await TrySendBacksAsync(run, text);
            if (error is null)
            {
                FinishBacks(run, text);
                return PrintOutcome.Sent;
            }

            session.PrintStatus = $"The fronts of {text.Phrase} were sent to the printer, but the backs could not be sent to {printerName}.";
            if (!prompts.ConfirmRetryBacks(session.PrintStatus + " " + error + TryBacksAgain))
            {
                return PrintOutcome.FrontsOnly;
            }
        }
    }

    /// <summary>Sends the backs; returns null on success, otherwise the text explaining the failure.</summary>
    private async Task<string?> TrySendBacksAsync(ManualRun run, PartText text)
    {
        var printerName = run.Target.PrinterName;
        try
        {
            session.PrintStatus = $"Sending the backs of {text.Phrase} to {printerName}...";
            var backs = ManualDuplexPlan.Build(run.Sheets, OptionsFor(run.Part)).Backs;
            await printer.PrintFacesAsync(printerName, run.Target.Paper, backs, run.Target.BookName + text.JobSuffix + BacksSuffix);
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return PrintErrors.BacksFailure(ex, printerName);
        }
    }

    private void FinishBacks(ManualRun run, PartText text)
    {
        var isTestSheet = run.Part == PrintPart.TestSheet;
        var sent = $"Sent the fronts and backs of {text.Phrase} to {run.Target.PrinterName}.";
        session.PrintStatus = isTestSheet ? sent : sent + $" If the backs came out wrong, change the options and use \"{PrintBacksAgainLabel}\".";
        if (isTestSheet && prompts.AskBackUpsideDown())
        {
            session.RotateBacks = !session.RotateBacks;
            session.PrintStatus = $"Rotate backs 180° is now {(session.RotateBacks ? "on" : "off")}. Print the test sheet again to check.";
        }
    }

    private sealed record ManualRun(PrintTarget Target, IReadOnlyList<Sheet> Sheets, PrintPart Part);
}
