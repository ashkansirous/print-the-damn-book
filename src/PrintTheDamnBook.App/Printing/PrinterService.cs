using System.ComponentModel;
using System.Printing;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using System.Windows.Xps;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Printing;

/// <summary>Lists printers and sends sheets to one, as a duplex job or as single-sided passes. Orientation, duplex and paper are always set on the ticket.</summary>
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

    /// <summary>Whether the printer reports short-edge duplex; null when it could not be checked.</summary>
    public Task<bool?> ProbeShortEdgeDuplexAsync(string printerName)
    {
        return StaThread.Run(() => ProbeShortEdgeDuplex(printerName));
    }

    /// <summary>Prints the sheets as one duplex job (front, back, front, back, ...) on a worker thread.</summary>
    public Task PrintAsync(string printerName, PaperKind paper, IReadOnlyList<Sheet> sheets, string jobName)
    {
        return StaThread.Run(() => Send(printerName, paper, jobName, RequiredDuplex, renderer => renderer.Render(sheets)));
    }

    /// <summary>Prints the faces as one single-sided job (manual two-sided printing) on a worker thread.</summary>
    public Task PrintFacesAsync(string printerName, PaperKind paper, IReadOnlyList<PlannedFace> faces, string jobName)
    {
        return StaThread.Run(() => Send(printerName, paper, jobName, Duplexing.OneSided, renderer => renderer.Render(faces)));
    }

    /// <summary>Checks that every image of the faces is readable, so a problem is found before the first pass prints.</summary>
    public Task CheckReadableAsync(IReadOnlyList<PlannedFace> faces)
    {
        return StaThread.Run(() => FaceRenderer.CheckReadable(faces));
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

    private static bool SupportsRequiredDuplex(PrintQueue queue)
    {
        return queue.GetPrintCapabilities().DuplexingCapability.Contains(RequiredDuplex);
    }

    private static bool? ProbeShortEdgeDuplex(string printerName)
    {
        try
        {
            using var server = new LocalPrintServer();
            using var queue = server.GetPrintQueue(printerName);
            return SupportsRequiredDuplex(queue);
        }
        catch (Exception ex) when (IsPrintingError(ex))
        {
            return null;
        }
    }

    private static void Send(string printerName, PaperKind paper, string jobName, Duplexing duplexing, Func<FaceRenderer, FixedDocument> render)
    {
        try
        {
            using var server = new LocalPrintServer();
            using var queue = OpenQueue(server, printerName);
            var ticket = BuildTicket(queue, paper, duplexing);
            var document = render(new FaceRenderer(paper, HalfSheetLayout.DefaultMarginMm));
            queue.CurrentJobSettings.Description = jobName;
            WriteJob(queue, document, ticket, printerName);
        }
        catch (Exception ex) when (IsPrintingError(ex))
        {
            throw new PrintFailedException($"Printing to {printerName} failed: {ex.Message}", ex);
        }
    }

    /// <summary>Hands the document to the spooler; a failure from here on may have left pages in the printer.</summary>
    private static void WriteJob(PrintQueue queue, FixedDocument document, PrintTicket ticket, string printerName)
    {
        try
        {
            PrintQueue.CreateXpsDocumentWriter(queue).Write(document, ticket);
        }
        catch (Exception ex) when (IsPrintingError(ex))
        {
            throw new PrintFailedException($"Printing to {printerName} failed: {ex.Message}", ex, mayHavePrinted: true);
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

    private static PrintTicket BuildTicket(PrintQueue queue, PaperKind paper, Duplexing duplexing)
    {
        if (duplexing == RequiredDuplex && !SupportsRequiredDuplex(queue))
        {
            throw new PrintFailedException($"{queue.FullName} does not support two-sided printing with a short-edge flip. Choose manual two-sided printing instead.");
        }

        var requested = new PrintTicket
        {
            PageOrientation = PageOrientation.Landscape,
            Duplexing = duplexing,
            PageMediaSize = new PageMediaSize(ToMediaSizeName(paper)),
        };
        var validated = queue.MergeAndValidatePrintTicket(new PrintTicket(), requested).ValidatedPrintTicket;
        EnsureHonoured(queue, validated, paper, duplexing);
        return validated;
    }

    /// <summary>
    /// A two-sided job needs the validated ticket to keep the requested duplexing. A one-sided job is refused when the
    /// queue turns it into a two-sided one (it would print the fronts on both sides of the paper); a queue with no duplex
    /// feature drops the setting (null), which is fine.
    /// </summary>
    private static bool IsDuplexHonoured(Duplexing? validated, Duplexing requested)
    {
        if (requested == Duplexing.OneSided)
        {
            return validated is null or Duplexing.OneSided;
        }

        return validated == requested;
    }

    private static void EnsureHonoured(PrintQueue queue, PrintTicket validated, PaperKind paper, Duplexing duplexing)
    {
        if (!IsDuplexHonoured(validated.Duplexing, duplexing))
        {
            throw new PrintFailedException($"{queue.FullName} would not accept the two-sided setting for this job.");
        }

        if (validated.PageOrientation != PageOrientation.Landscape)
        {
            throw new PrintFailedException($"{queue.FullName} would not accept landscape orientation.");
        }

        if (!IsRequestedPaper(validated.PageMediaSize, paper))
        {
            throw new PrintFailedException($"{queue.FullName} does not support {paper} paper.");
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
