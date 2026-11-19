using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp.Pictures;

namespace PrintTheDamnBook.Kdp;

/// <summary>What the command line asked for: one local <c>.dino</c>, the language to print it in and the folder to write to.</summary>
/// <param name="Offsets">Crop offsets (story page number to pixels) from <c>--offset</c>, saved to <c>kdp.json</c>; null when none were given.</param>
public sealed record KdpExportRequest(string DinoPath, string Language, string OutFolder, IReadOnlyDictionary<int, int>? Offsets = null);

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
        Directory.CreateDirectory(request.OutFolder);
        var storyPages = Math.Min(book.Pages.Count, pictures.Count);
        var settings = MergeOffsets(request, book, storyPages);
        var input = new KdpBookInput(
            book.In(book.Title, language.Code),
            language.Code,
            book.BackCover?.Credits,
            DateTime.UtcNow.Year,
            book.Pages.Select(p => StoryLines.Clean(book.In(p.Text, language.Code))).ToList(),
            pictures,
            settings);
        var built = Build(input);
        var warnings = new List<string>([.. BookWarnings(book, language), .. StaleOffsetWarnings(settings, storyPages), .. built.Warnings]);
        var pdfPath = Path.Combine(request.OutFolder, PdfName);
        var reportPath = Path.Combine(request.OutFolder, ReportName);
        var result = built with { Warnings = warnings };
        var toSave = OffsetsToSave(request, book, built.Pictures);
        WriteBoth(pdfPath, result.Pdf, reportPath, KdpCheckReport.Build(input.Title, language.Code, result));
        WritePreviews(request.OutFolder, built.Pictures);
        SaveOffsets(request, toSave);
        return new KdpExportResult(pdfPath, reportPath, result.Plan.PageCount, result.Warnings);
    }

    /// <summary>The owner's choices in <c>kdp.json</c> beside the output, with any <c>--offset</c> of this run added (0 clears the page's choice); nothing is saved yet. A file that cannot be read stops the run and is left as it is.</summary>
    private static KdpBookSettings MergeOffsets(KdpExportRequest request, DinoBook book, int storyPages)
    {
        try
        {
            var settings = ReadStored(request, book);
            foreach (var (page, pixels) in request.Offsets ?? new Dictionary<int, int>())
            {
                if (page < 1 || page > storyPages)
                {
                    throw new KdpException($"--offset names page {page}, but the book has {storyPages} story pages with a picture.");
                }

                settings = pixels == 0 ? settings.Without(page) : settings.WithOffset(page, pixels);
            }

            return settings;
        }
        catch (KdpSettingsException ex)
        {
            throw new KdpException(ex.Message, ex);
        }
    }

    /// <summary>The file as it is on disk; one that was made for another book stops the run (<see cref="KdpSettingsException"/>) and is never overwritten.</summary>
    private static KdpBookSettings ReadStored(KdpExportRequest request, DinoBook book)
    {
        var stored = KdpBookSettings.Read(SettingsPath(request));
        stored.EnsureBook(book.Id, book.Title);
        return stored;
    }

    private static string SettingsPath(KdpExportRequest request) => Path.Combine(request.OutFolder, KdpBookSettings.FileName);

    /// <summary>Saved entries for pages the book does not have are ignored by the build; say so, so a stale file is not a silent surprise.</summary>
    private static IEnumerable<string> StaleOffsetWarnings(KdpBookSettings settings, int storyPages)
    {
        foreach (var page in settings.Pages.Where(p => p > storyPages))
        {
            yield return $"{KdpBookSettings.FileName} has an offset for story page {page}, but the book has {storyPages} story pages; it is ignored.";
        }
    }

    /// <summary>
    /// Works out what this run's <c>--offset</c> choices make of <c>kdp.json</c>, in memory: only offsets that moved something (the value actually used) and
    /// clears, with the book's identity. Null when nothing changes. An offset with no effect is not saved; <c>KdpInterior</c> already warns about it once.
    /// </summary>
    private static KdpBookSettings? OffsetsToSave(KdpExportRequest request, DinoBook book, IReadOnlyList<KdpPicture> pictures)
    {
        if (request.Offsets is not { Count: > 0 } offsets)
        {
            return null;
        }

        try
        {
            var before = ReadStored(request, book);
            var settings = before;
            foreach (var (page, pixels) in offsets)
            {
                if (pixels == 0)
                {
                    settings = settings.Without(page);
                }
                else if (pictures[page - 1].MaxOffsetPx > 0)
                {
                    settings = settings.WithOffset(page, pictures[page - 1].AppliedOffsetPx);
                }
            }

            return settings.ToJson() == before.ToJson() ? null : settings.ForBook(book.Id, book.Title);
        }
        catch (KdpSettingsException ex)
        {
            throw new KdpException(ex.Message, ex);
        }
    }

    /// <summary>The last write of a run: <c>kdp.json</c> is touched only after the PDF, report and previews are in place, so a failure earlier leaves the old file as it was.</summary>
    private static void SaveOffsets(KdpExportRequest request, KdpBookSettings? settings)
    {
        try
        {
            settings?.Write(SettingsPath(request));
        }
        catch (KdpSettingsException ex)
        {
            throw new KdpException(ex.Message, ex);
        }
    }

    /// <summary>The previews of this run's flagged pages go to <c>previews/</c>; those of an earlier run are removed first so none is stale.</summary>
    private static void WritePreviews(string outFolder, IReadOnlyList<KdpPicture> pictures)
    {
        var folder = Path.Combine(outFolder, Path.GetDirectoryName(CutLineInputs.PreviewPath(1))!);
        if (Directory.Exists(folder))
        {
            foreach (var old in Directory.GetFiles(folder, "page-*.jpg"))
            {
                File.Delete(old);
            }
        }

        for (var i = 0; i < pictures.Count; i++)
        {
            if (pictures[i].Preview is { } preview)
            {
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(outFolder, CutLineInputs.PreviewPath(i + 1)), preview);
            }
        }
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
