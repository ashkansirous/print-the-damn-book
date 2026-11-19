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
            output.WriteLine($"Wrote {result.PdfPath} ({result.PageCount} pages) and {result.ReportPath}.");
            foreach (var warning in result.Warnings)
            {
                error.WriteLine($"Warning: {warning}");
            }

            return Ok;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error.WriteLine($"Error: {ex.Message}");
            return Failed;
        }
    }
}
