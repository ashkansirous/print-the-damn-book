using PrintTheDamnBook.Kdp;

namespace PrintTheDamnBook.Cli;

/// <summary>Runs one command line and returns the exit code: 0 on success, 1 when the book could not be made, 2 for wrong arguments.</summary>
public static class KdpRunner
{
    public const int Ok = 0;
    public const int Failed = 1;
    public const int BadArguments = 2;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args[0] != KdpCommandLine.Command)
        {
            error.WriteLine(KdpCommandLine.Usage);
            return BadArguments;
        }

        var parsed = KdpCommandLine.Parse(args.Skip(1).ToList());
        if (parsed.Request is not { } request)
        {
            error.WriteLine(parsed.Error);
            error.WriteLine(KdpCommandLine.Usage);
            return BadArguments;
        }

        return Export(request, output, error);
    }

    private static int Export(KdpExportRequest request, TextWriter output, TextWriter error)
    {
        try
        {
            error.WriteLine($"Building {Path.GetFileName(request.DinoPath)} ...");
            var result = KdpExporter.Export(request);
            var written = result.CoverError is null ? $"{result.PdfPath} ({result.PageCount} pages), {result.CoverPath} and {result.ReportPath}" : $"{result.PdfPath} ({result.PageCount} pages) and {result.ReportPath}";
            output.WriteLine($"Wrote {written}.");
            if (result.ContactSheetPath is { } sheet)
            {
                output.WriteLine($"Wrote the contact sheet {sheet} and small page previews beside it.");
            }

            if (result.CropSheetPath is { } cropSheet)
            {
                output.WriteLine($"Wrote the crop sheet {cropSheet} and the larger uncut copies beside it.");
            }

            if (result.OtherBookTitles is { Count: > 0 } others)
            {
                output.WriteLine($"Other books on the last page ({others.Count}): {string.Join("; ", others)}.");
            }

            foreach (var warning in result.Warnings)
            {
                error.WriteLine($"Warning: {warning}");
            }

            if (result.CoverError is { } coverError)
            {
                if (result.CoverNote is { } note)
                {
                    error.WriteLine($"Note: {note}");
                }

                error.WriteLine($"Error: cover.pdf was not written: {coverError}");
            }

            if (result.PreviewError is { } previewError)
            {
                error.WriteLine($"Error: the previews were not written: {previewError}");
            }

            if (result.CropSheetError is { } cropSheetError)
            {
                error.WriteLine($"Error: the crop sheet was not written: {cropSheetError}");
            }

            var bookErrors = result.Errors ?? [];
            foreach (var bookError in bookErrors)
            {
                error.WriteLine($"Error: {bookError}");
            }

            return result.CoverError is null && result.PreviewError is null && result.CropSheetError is null && bookErrors.Count == 0 ? Ok : Failed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error.WriteLine($"Error: {ex.Message}");
            return Failed;
        }
    }
}
