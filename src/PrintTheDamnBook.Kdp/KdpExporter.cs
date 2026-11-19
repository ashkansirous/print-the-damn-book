using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Kdp;

/// <summary>What the command line asked for: one local <c>.dino</c>, the language to print it in and the folder to write to.</summary>
public sealed record KdpExportRequest(string DinoPath, string Language, string OutFolder);

/// <summary>The files written and what to tell the person who ran the command.</summary>
public sealed record KdpExportResult(string PdfPath, string ReportPath, int PageCount, IReadOnlyList<string> Warnings);

/// <summary><c>.dino</c> in, <c>interior.pdf</c> and <c>check-report.md</c> out (Slice 27).</summary>
public static class KdpExporter
{
    public const string PdfName = "interior.pdf";
    public const string ReportName = "check-report.md";

    public static KdpExportResult Export(KdpExportRequest request)
    {
        var book = ReadBook(request.DinoPath);
        var language = ChooseLanguage(book, request.Language);
        var pictures = ReadPictures(request.DinoPath, book);
        var input = new KdpBookInput(
            book.In(book.Title, language.Code),
            language.Code,
            book.BackCover?.Credits,
            DateTime.UtcNow.Year,
            book.Pages.Select(p => StoryLines.Clean(book.In(p.Text, language.Code))).ToList(),
            pictures);
        var built = Build(input);
        var result = built with { Warnings = [.. BookWarnings(book, language), .. built.Warnings] };

        Directory.CreateDirectory(request.OutFolder);
        var pdfPath = Path.Combine(request.OutFolder, PdfName);
        var reportPath = Path.Combine(request.OutFolder, ReportName);
        WriteBoth(pdfPath, result.Pdf, reportPath, KdpCheckReport.Build(input.Title, language.Code, result));
        return new KdpExportResult(pdfPath, reportPath, result.Plan.PageCount, result.Warnings);
    }

    /// <summary>Both files go to temporary names first and are moved into place together, so a failed write never leaves a new PDF beside an old report; a cleanup failure never hides the first error.</summary>
    private static void WriteBoth(string pdfPath, byte[] pdf, string reportPath, string report)
    {
        var pdfTemp = pdfPath + ".tmp";
        var reportTemp = reportPath + ".tmp";
        try
        {
            File.WriteAllBytes(pdfTemp, pdf);
            File.WriteAllText(reportTemp, report);
            File.Move(pdfTemp, pdfPath, overwrite: true);
            File.Move(reportTemp, reportPath, overwrite: true);
        }
        finally
        {
            TryDelete(pdfTemp);
            TryDelete(reportTemp);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temporary file is not worth hiding the real error.
        }
    }

    /// <summary>What the book itself costs the PDF: pages dropped on reading (the story is incomplete) and a language the front matter is not written in.</summary>
    private static IEnumerable<string> BookWarnings(DinoBook book, DinoLanguage language)
    {
        if (book.SkippedPages > 0)
        {
            yield return $"{book.SkippedPages} page(s) of the book were skipped because their picture could not be used; the story in this PDF is incomplete.";
        }

        if (DinoBook.Primary(language.Code) != KdpFrontMatterWording.English)
        {
            yield return $"The story is set in \"{language.Code}\" but the title page, dedication, end page and about page are English only in this version.";
        }
    }

    /// <summary>QuestPDF reports a missing glyph or an impossible layout by exception; both become a plain <see cref="KdpException"/>.</summary>
    private static KdpInteriorResult Build(KdpBookInput input)
    {
        try
        {
            return KdpInterior.Build(input);
        }
        catch (Exception ex) when (ex is QuestPDF.Drawing.Exceptions.DocumentDrawingException or QuestPDF.Drawing.Exceptions.DocumentLayoutException)
        {
            throw new KdpException($"The PDF could not be drawn: {ex.Message}", ex);
        }
    }

    private static DinoBook ReadBook(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return DinoBookReader.Read(stream);
        }
        catch (DinoBookException ex)
        {
            throw new KdpException(ex.Message, ex);
        }
        catch (IOException ex)
        {
            throw new KdpException($"The book could not be read: {ex.Message}", ex);
        }
    }

    private static DinoLanguage ChooseLanguage(DinoBook book, string code)
    {
        var languages = book.Languages();
        var wanted = DinoBook.Primary(code);
        var found = languages.FirstOrDefault(l => DinoBook.Primary(l.Code) == wanted)
            ?? throw new KdpException($"The book has no \"{code}\" text. It has: {string.Join(", ", languages.Select(l => l.Code))}.");
        return found.IsSupported ? found : throw new KdpException($"{found.Name} cannot be set in type yet.");
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: it is in the temp folder; a failed cleanup must not hide the real error.
        }
    }

    /// <summary>The pictures are pulled out through <see cref="DinoBookReader"/> (its caps and name checks) into a temporary folder that is removed afterwards.</summary>
    private static List<byte[]> ReadPictures(string path, DinoBook book)
    {
        var folder = Path.Combine(Path.GetTempPath(), "ptdb-kdp-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var stream = File.OpenRead(path);
            var extracted = DinoBookReader.ExtractPictures(stream, book, folder);
            return extracted.PageFiles.Select(file => File.ReadAllBytes(Path.Combine(folder, file))).ToList();
        }
        catch (DinoBookException ex)
        {
            throw new KdpException(ex.Message, ex);
        }
        finally
        {
            TryDeleteFolder(folder);
        }
    }
}
