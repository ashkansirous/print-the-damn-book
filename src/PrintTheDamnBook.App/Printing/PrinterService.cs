using System.ComponentModel;
using System.Printing;
using System.Runtime.InteropServices;
using System.Windows.Xps;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Printing;

/// <summary>Lists printers and sends sheets to one as a duplex job. Orientation, duplex and paper are always set on the ticket.</summary>
public sealed class PrinterService
{
    private const Duplexing RequiredDuplex = Duplexing.TwoSidedShortEdge;

    private const double MediaToleranceMm = 2;

    /// <summary>True for the errors the spooler, drivers and XPS writer raise; the caller reports them instead of crashing.</summary>
    public static bool IsPrintingError(Exception ex)
    {
        return ex is PrintQueueException or PrintSystemException or PrintServerException or XpsException
            or InvalidOperationException or System.IO.IOException or COMException or Win32Exception;
    }

    public Task<IReadOnlyList<PrinterChoice>> ListPrintersAsync()
    {
        return StaThread.Run(ListPrinters);
    }

    /// <summary>Prints the sheets as one duplex job (front, back, front, back, ...) on a worker thread.</summary>
    public Task PrintAsync(string printerName, PaperKind paper, IReadOnlyList<Sheet> sheets, string jobName)
    {
        return StaThread.Run(() => Print(printerName, paper, sheets, jobName));
    }

    private static IReadOnlyList<PrinterChoice> ListPrinters()
    {
        using var server = new LocalPrintServer();
        var defaultName = TryGetDefaultName();
        var types = new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections };
        using var queues = server.GetPrintQueues(types);
        var choices = new List<PrinterChoice>();
        foreach (var queue in queues)
        {
            choices.Add(new PrinterChoice(queue.FullName, queue.FullName == defaultName));
            queue.Dispose();
        }

        return choices.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static void Print(string printerName, PaperKind paper, IReadOnlyList<Sheet> sheets, string jobName)
    {
        try
        {
            using var server = new LocalPrintServer();
            using var queue = OpenQueue(server, printerName);
            var ticket = BuildTicket(queue, paper);
            var document = new FaceRenderer(paper, HalfSheetLayout.DefaultMarginMm).Render(sheets);
            queue.CurrentJobSettings.Description = jobName;
            PrintQueue.CreateXpsDocumentWriter(queue).Write(document, ticket);
        }
        catch (Exception ex) when (IsPrintingError(ex))
        {
            throw new PrintFailedException($"Printing to {printerName} failed: {ex.Message}", ex);
        }
    }

    private static string? TryGetDefaultName()
    {
        try
        {
            using var queue = LocalPrintServer.GetDefaultPrintQueue();
            return queue.FullName;
        }
        catch (Exception ex) when (IsPrintingError(ex))
        {
            return null;
        }
    }

    private static PrintQueue OpenQueue(PrintServer server, string printerName)
    {
        var queue = server.GetPrintQueue(printerName);
        queue.Refresh();
        if (queue.IsOffline || queue.IsNotAvailable)
        {
            queue.Dispose();
            throw new PrintFailedException($"{printerName} is offline or not available. Check that it is switched on and connected.");
        }

        return queue;
    }

    private static PrintTicket BuildTicket(PrintQueue queue, PaperKind paper)
    {
        var capabilities = queue.GetPrintCapabilities();
        if (!capabilities.DuplexingCapability.Contains(RequiredDuplex))
        {
            throw new PrintFailedException($"{queue.FullName} does not support two-sided printing with a short-edge flip, so the booklet cannot be printed on it. Nothing was printed.");
        }

        var requested = new PrintTicket
        {
            PageOrientation = PageOrientation.Landscape,
            Duplexing = RequiredDuplex,
            PageMediaSize = new PageMediaSize(ToMediaSizeName(paper)),
        };
        var validated = queue.MergeAndValidatePrintTicket(new PrintTicket(), requested).ValidatedPrintTicket;
        EnsureHonoured(queue, validated, paper);
        return validated;
    }

    private static void EnsureHonoured(PrintQueue queue, PrintTicket validated, PaperKind paper)
    {
        if (validated.Duplexing != RequiredDuplex)
        {
            throw new PrintFailedException($"{queue.FullName} would not accept two-sided printing with a short-edge flip. Nothing was printed.");
        }

        if (validated.PageOrientation != PageOrientation.Landscape)
        {
            throw new PrintFailedException($"{queue.FullName} would not accept landscape orientation. Nothing was printed.");
        }

        if (!IsRequestedPaper(validated.PageMediaSize, paper))
        {
            throw new PrintFailedException($"{queue.FullName} does not support {paper} paper. Nothing was printed.");
        }
    }

    /// <summary>Accepts the paper by its dimensions (drivers vary the size name); falls back to the name when no dimensions are given.</summary>
    private static bool IsRequestedPaper(PageMediaSize? media, PaperKind paper)
    {
        if (media?.Width is not { } width || media.Height is not { } height)
        {
            return media?.PageMediaSizeName == ToMediaSizeName(paper);
        }

        var candidateMm = new SizeD(width / Paper.DipsPerMillimetre, height / Paper.DipsPerMillimetre);
        return Paper.MatchesSize(paper, candidateMm, MediaToleranceMm);
    }

    private static PageMediaSizeName ToMediaSizeName(PaperKind paper)
    {
        return paper switch
        {
            PaperKind.A4 => PageMediaSizeName.ISOA4,
            PaperKind.Letter => PageMediaSizeName.NorthAmericaLetter,
            _ => throw new ArgumentOutOfRangeException(nameof(paper), paper, null),
        };
    }
}
