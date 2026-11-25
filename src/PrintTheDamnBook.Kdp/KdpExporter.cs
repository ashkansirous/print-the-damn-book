using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp.Cover;
using PrintTheDamnBook.Kdp.CropSheet;
using PrintTheDamnBook.Kdp.Pictures;
using PrintTheDamnBook.Kdp.Previews;
using PrintTheDamnBook.Kdp.StyleSheet;
using PrintTheDamnBook.Kdp.Styling;

namespace PrintTheDamnBook.Kdp;

/// <summary>What the command line asked for: one local <c>.dino</c>, the language to print it in and the folder to write to.</summary>
/// <param name="Offsets">Crop offsets (story page number to pixels) from <c>--offset</c>, saved to <c>kdp.json</c>; null when none were given.</param>
/// <param name="Previews">True for <c>--previews</c> (Slice 30): also write small per-page PNGs of the interior and a contact sheet.</param>
/// <param name="CoverOffsets">Crop offsets of the cover pictures (<see cref="KdpPictureKeys.Front"/> or <see cref="KdpPictureKeys.Back"/> to pixels) from <c>--offset front=...</c>; null when none were given.</param>
/// <param name="CropSheet">True for <c>--crop-sheet</c> (Slice 36): also write the crop sheet and the larger uncut copies for the crop agent.</param>
/// <param name="CoverText">The crop agent's text box and colours per cover key (<c>--text front=...</c>), saved to <c>kdp.json</c>; a null value clears the choice (<c>--text front=clear</c>); null when none were given (Slice 37).</param>
/// <param name="DinoText">The colours (and, since Slice 41, the box) of the Dino line beside the QR code (<c>--dino-text back=...</c>); a change with null colours clears them; null when not given (Slice 37).</param>
/// <param name="OtherBooksFolder">The folder of <c>.dino</c> files whose other books are shown on the last page (<c>--other-books</c>, Slice 41); null leaves the last page as it was.</param>
/// <param name="BooksOutRoot">The folder that holds each book's output folder, where their <c>kdp.json</c> files are (<c>--books-out</c>); null means the parent of the output folder.</param>
/// <param name="Styles">The per-book style choices of this run (<c>--speaker</c>, <c>--main-character</c>, <c>--title-picture</c>), saved to <c>kdp.json</c> <c>styles</c>; null when none were given (Slice 46).</param>
public sealed record KdpExportRequest(
    string DinoPath,
    string Language,
    string OutFolder,
    IReadOnlyDictionary<int, int>? Offsets = null,
    bool Previews = false,
    IReadOnlyDictionary<string, int>? CoverOffsets = null,
    bool CropSheet = false,
    IReadOnlyDictionary<string, KdpCoverText?>? CoverText = null,
    KdpDinoTextChange? DinoText = null,
    string? OtherBooksFolder = null,
    string? BooksOutRoot = null,
    KdpStyleChanges? Styles = null,
    bool StyleSheet = false);

/// <summary>A <c>--dino-text</c> request: the colours to save, or null to clear the whole choice; with <paramref name="Box"/> also the box the Dino block (QR code and line) sits in (Slice 41).</summary>
public sealed record KdpDinoTextChange(KdpTextColors? Colors, KdpTextBox? Box = null);

/// <summary>The files written and what to tell the person who ran the command.</summary>
public sealed record KdpExportResult(string PdfPath, string ReportPath, int PageCount, IReadOnlyList<string> Warnings, string CoverPath = "", string? CoverError = null, string? CoverNote = null, string? ContactSheetPath = null, string? PreviewError = null, string? CropSheetPath = null, string? CropSheetError = null, IReadOnlyList<string>? OtherBookTitles = null, IReadOnlyList<string>? Errors = null, string? StyleSheetPath = null, string? StyleSheetError = null);

/// <summary>The pictures pulled out of a book: one per story page, the cover and the back cover (each null when the book has none).</summary>
internal sealed record BookPictures(List<byte[]> Pages, byte[]? Cover, byte[]? Back);

/// <summary><c>.dino</c> in, <c>interior.pdf</c>, <c>cover.pdf</c> and <c>check-report.md</c> out (Slices 27 and 29).</summary>
public static class KdpExporter
{
    public const string PdfName = "interior.pdf";
    public const string CoverName = "cover.pdf";
    public const string PreviousCoverName = "cover-previous.pdf";
    public const string ReportName = "check-report.md";
    public const string NoCoverWarning = "cover: the book has no cover picture; its first story picture is used on the front";

    public static KdpExportResult Export(KdpExportRequest request) => Export(request, KdpStyleSheetWriter.Write);

    /// <summary>The export with the style-sheet writer given, so a test can make that side output fail in a way no file system gives.</summary>
    internal static KdpExportResult Export(KdpExportRequest request, Func<string, KdpBookInput, KdpInteriorResult, string> writeStyleSheet)
    {
        var book = ReadBook(request.DinoPath);
        var language = ChooseLanguage(book, request.Language);
        var all = ReadPictures(request.DinoPath, book);
        var pictures = all.Pages;
        Directory.CreateDirectory(request.OutFolder);
        var storyPages = Math.Min(book.Pages.Count, pictures.Count);
        var settings = MergeOffsets(request, book, language, storyPages, all);
        var others = LoadOtherBooks(request, book, language);
        var input = BookInput(book, language, pictures, settings, others);
        var built = Build(input);
        EnsureRequestedSpeakersAccepted(request, built);
        var (cover, coverError) = TryBuildCover(book, language, all, built.Plan.PageCount, settings);
        var coverPath = Path.Combine(request.OutFolder, CoverName);
        var warnings = new List<string>([.. BookWarnings(book, language), .. StaleOffsetWarnings(settings, storyPages, all.Back is not null), .. built.Warnings, .. cover?.Warnings ?? [], .. UnsavedCoverOffsetWarnings(request, cover), .. others?.Warnings ?? [], .. UnusedSpeakerWarnings(request, built)]);
        var pdfPath = Path.Combine(request.OutFolder, PdfName);
        var reportPath = Path.Combine(request.OutFolder, ReportName);
        var result = built with { Warnings = warnings };
        var (cropSheet, cropSheetError) = TryWriteCropSheet(request, all, settings, storyPages);
        if (!request.CropSheet && cropSheetError is not null)
        {
            warnings.Add($"the crop sheet that --style-sheet also writes was not written: {cropSheetError}");
            cropSheetError = null;
        }

        var toSave = OffsetsToSave(request, book, language, built.Pictures, cover);
        var pdfs = cover is null ? [(pdfPath, result.Pdf)] : new[] { (pdfPath, result.Pdf), (coverPath, cover.Pdf) };
        var older = WriteAll(pdfs, reportPath, cover is null ? coverPath : null, stale => KdpCheckReport.Build(input.Title, language.Code, result, cover, coverError, stale));
        WritePreviews(request.OutFolder, built.Pictures);
        var (sheet, previewError) = TryWritePreviews(request, input, result);
        var (styleSheet, styleSheetError) = TryWriteStyleSheet(request, input, result, writeStyleSheet);
        SaveOffsets(request, toSave);
        return new KdpExportResult(pdfPath, reportPath, result.Plan.PageCount, result.Warnings, cover is null ? string.Empty : coverPath, coverError, KdpCheckReport.StaleText(older), sheet, previewError, cropSheet, cropSheetError, others?.Tiles.Select(t => t.Title).ToList(), result.Errors, styleSheet, styleSheetError);
    }

    /// <summary>
    /// <c>--style-sheet</c> (Slice 46 item H): <c>styles/speakers.md</c> for the style agent, written after the build, from its result; without the option the file of an earlier
    /// run is removed so none is stale.
    /// </summary>
    private static (string? Path, string? Error) TryWriteStyleSheet(KdpExportRequest request, KdpBookInput input, KdpInteriorResult result, Func<string, KdpBookInput, KdpInteriorResult, string> write)
    {
        try
        {
            KdpStyleSheetWriter.Remove(request.OutFolder);
            return request.StyleSheet ? (write(request.OutFolder, input, result), null) : (null, null);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException or OperationCanceledException))
        {
            return (null, ex.Message);
        }
    }

    private static KdpBookInput BookInput(DinoBook book, DinoLanguage language, List<byte[]> pictures, KdpBookSettings settings, KdpOtherBooksResult? others)
    {
        return new KdpBookInput(
            book.In(book.Title, language.Code),
            language.Code,
            book.BackCover?.Credits,
            DateTime.UtcNow.Year,
            book.Pages.Select(p => StoryLines.Clean(book.In(p.Text, language.Code))).ToList(),
            pictures,
            settings,
            others?.Tiles.Select(t => t.Jpeg).ToList(),
            book.CastIn(language.Code));
    }

    /// <summary>
    /// A speaker colour given on the command line that fails the colour rules once the book's page tints and the other speakers are known (the cheaper rules were checked
    /// when it was read) stops the run before anything is written, with the reason; <c>kdp.json</c> is left as it was.
    /// </summary>
    private static void EnsureRequestedSpeakersAccepted(KdpExportRequest request, KdpInteriorResult built)
    {
        foreach (var (name, color) in request.Styles?.Speakers ?? new Dictionary<string, string?>())
        {
            if (color is not null && built.Style is { } style && style.RejectedSpeakers.TryGetValue(name, out var why))
            {
                throw new KdpException($"--speaker {name}={color} is refused: {why}");
            }
        }
    }

    /// <summary>A <c>--speaker</c> colour for a cast name the book does not have as a speaking character is saved but never drawn; the report names it so it is not a silent no-op.</summary>
    private static IEnumerable<string> UnusedSpeakerWarnings(KdpExportRequest request, KdpInteriorResult built)
    {
        if (built.Style is not { } style)
        {
            yield break;
        }

        foreach (var (name, color) in request.Styles?.Speakers ?? new Dictionary<string, string?>())
        {
            if (color is not null && !style.SpeakerColours.ContainsKey(name) && !style.RejectedSpeakers.ContainsKey(name))
            {
                yield return $"styles: --speaker {name}={color} was saved, but \"{name}\" is not a speaking character found in the story, so the colour is not used";
            }
        }
    }

    /// <summary>
    /// The other books for the last page (Slice 41); null without <c>--other-books</c>. The books' own <c>kdp.json</c> files are looked for in <c>--books-out</c>, else the parent of the output folder.
    /// A folder that is missing or cannot be read never stops the export: it is a warning in the report and the last page is drawn without other books.
    /// </summary>
    private static KdpOtherBooksResult? LoadOtherBooks(KdpExportRequest request, DinoBook book, DinoLanguage language)
    {
        if (request.OtherBooksFolder is not { } folder)
        {
            return null;
        }

        var root = request.BooksOutRoot ?? OutputsRootOf(request.OutFolder);
        try
        {
            return KdpOtherBooksLoader.Load(folder, root, book.Id, language.Code);
        }
        catch (Exception ex) when (ex is KdpException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new KdpOtherBooksResult([], [$"other books: {ex.Message}; the last page shows the QR code and line only"]);
        }
    }

    /// <summary>The folder that holds every book's output folder: the parent of <paramref name="outFolder"/>, whether or not it was given with a trailing separator.</summary>
    internal static string OutputsRootOf(string outFolder)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outFolder));
        return Path.GetDirectoryName(full) ?? full;
    }

    /// <summary>
    /// <c>--crop-sheet</c> (Slice 36) is a side output like the previews: a failure to make it is reported and never stops the PDFs, the report or <c>kdp.json</c>.
    /// Without the option, the files of an earlier run are removed so none shows a window that is no longer the one in use.
    /// </summary>
    private static (string? SheetPath, string? Error) TryWriteCropSheet(KdpExportRequest request, BookPictures all, KdpBookSettings settings, int storyPages)
    {
        try
        {
            KdpCropSheetWriter.Remove(request.OutFolder);
            return request.CropSheet || request.StyleSheet ? (KdpCropSheetWriter.Write(request.OutFolder, CropSheetPictures(all, settings, storyPages)).SheetPath, null) : (null, null);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException or OperationCanceledException))
        {
            return (null, ex.Message);
        }
    }

    /// <summary>Every picture of the book with the offset in use: the story pages, the front (the first story picture when the book has no cover) and the back when there is one.</summary>
    internal static List<KdpCropSheetPicture> CropSheetPictures(BookPictures all, KdpBookSettings settings, int storyPages)
    {
        var pictures = all.Pages.Take(storyPages).Select((bytes, i) => new KdpCropSheetPicture(KdpPictureKeys.ForStoryPage(i + 1), bytes, settings.OffsetFor(i + 1))).ToList();
        var front = all.Cover ?? (all.Pages.Count > 0 ? all.Pages[0] : null);
        if (front is not null)
        {
            pictures.Add(new KdpCropSheetPicture(KdpPictureKeys.Front, front, settings.CoverOffsetFor(KdpPictureKeys.Front)));
        }

        if (all.Back is not null)
        {
            pictures.Add(new KdpCropSheetPicture(KdpPictureKeys.Back, all.Back, settings.CoverOffsetFor(KdpPictureKeys.Back)));
        }

        return pictures;
    }

    /// <summary>
    /// The <c>--previews</c> files are a side output like the cover: a failure to make them is reported and never stops the PDFs, the report or <c>kdp.json</c>.
    /// Without the option, those of an earlier run are removed so none is stale; after a failure none is left either.
    /// </summary>
    private static (string? ContactSheet, string? Error) TryWritePreviews(KdpExportRequest request, KdpBookInput input, KdpInteriorResult result)
    {
        try
        {
            KdpPreviews.Remove(request.OutFolder);
            return (request.Previews ? KdpPreviews.Write(request.OutFolder, input, result).ContactSheetPath : null, null);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException or OperationCanceledException))
        {
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// The isolation boundary for the whole cover pipeline (picture decode, layout, rendering to bytes): a cover that cannot be made must not stop the
    /// interior and the report. The filter is broad on purpose, because decoders, SkiaSharp and QuestPDF throw many unrelated types; only process-fatal
    /// exceptions and cancellation propagate.
    /// </summary>
    private static (KdpCoverResult? Cover, string? Error) TryBuildCover(DinoBook book, DinoLanguage language, BookPictures all, int pageCount, KdpBookSettings settings)
    {
        try
        {
            return (BuildCover(book, language, all, pageCount, settings), null);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException or OperationCanceledException))
        {
            return (null, ex.Message);
        }
    }

    /// <summary>With no new cover, an older cover.pdf is renamed to cover-previous.pdf (replacing an older one of that name), never deleted; a rename that fails is reported, not thrown.</summary>
    private static StaleCover KeepOlderCover(string coverPath)
    {
        if (!File.Exists(coverPath))
        {
            return StaleCover.None;
        }

        try
        {
            File.Move(coverPath, Path.Combine(Path.GetDirectoryName(coverPath)!, PreviousCoverName), overwrite: true);
            return StaleCover.Renamed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return StaleCover.RenameFailed;
        }
    }

    /// <summary>The cover: the book's cover picture on the front (its first story picture when it has none, with a warning) and its back cover picture and credits behind.</summary>
    private static KdpCoverResult BuildCover(DinoBook book, DinoLanguage language, BookPictures all, int pageCount, KdpBookSettings settings)
    {
        var front = all.Cover ?? (all.Pages.Count > 0 ? all.Pages[0] : throw new KdpException("The book has no pictures, so there is nothing to put on the cover."));
        var direction = TextDirection.ForLanguage(language.Code);
        var input = new KdpCoverInput(book.In(book.Title, language.Code), language.Code, book.BackCover?.Credits, front, all.Back, pageCount, direction, settings);
        try
        {
            var result = KdpCover.Build(input);
            return all.Cover is null ? result with { Warnings = [NoCoverWarning, .. result.Warnings] } : result;
        }
        catch (Exception ex) when (ex is QuestPDF.Drawing.Exceptions.DocumentDrawingException or QuestPDF.Drawing.Exceptions.DocumentLayoutException)
        {
            throw new KdpException($"The cover PDF could not be drawn: {ex.Message}", ex);
        }
    }

    /// <summary>The owner's choices in <c>kdp.json</c> beside the output, with any <c>--offset</c> of this run added (0 clears the page's choice); nothing is saved yet. A file that cannot be read stops the run and is left as it is.</summary>
    private static KdpBookSettings MergeOffsets(KdpExportRequest request, DinoBook book, DinoLanguage language, int storyPages, BookPictures all)
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

            foreach (var (key, pixels) in request.CoverOffsets ?? new Dictionary<string, int>())
            {
                if (key == KdpPictureKeys.Back && all.Back is null && pixels != 0)
                {
                    throw new KdpException("--offset back names the back cover picture, but the book has none.");
                }

                settings = pixels == 0 ? settings.WithoutCover(key) : settings.WithCoverOffset(key, pixels);
            }

            settings = ApplyText(settings, request, all.Back is not null, key => UsedCoverOffset(key, settings, all));
            return ApplyStyles(settings, request, book, language, storyPages);
        }
        catch (KdpSettingsException ex)
        {
            throw new KdpException(ex.Message, ex);
        }
    }

    /// <summary>The crop offset a cover picture is really drawn at: the requested one, cut back to what the picture allows (0 when it has no spare height).</summary>
    private static int UsedCoverOffset(string key, KdpBookSettings settings, BookPictures all)
    {
        var bytes = key == KdpPictureKeys.Back ? all.Back : all.Cover ?? all.Pages.FirstOrDefault();
        var requested = settings.CoverOffsetFor(key);
        if (bytes is null || requested == 0)
        {
            return requested;
        }

        using var bitmap = KdpImageDecoder.DecodeUpright(bytes);
        return Math.Min(requested, KdpPictureCrop.MaxOffset(bitmap.Width, bitmap.Height));
    }

    /// <summary>The crop offset the built cover really used for a picture (the saved one cut back to what the picture allows), the same value the render path judges a text choice against.</summary>
    private static int AppliedCoverOffset(string key, KdpBookSettings settings, KdpCoverResult cover)
    {
        var picture = key == KdpPictureKeys.Front ? cover.FrontPrepared : cover.BackPrepared;
        return picture is not null && settings.HasCoverOffset(key) ? picture.AppliedOffsetPx : settings.CoverOffsetFor(key);
    }

    /// <summary>This run's <c>--text</c> and <c>--dino-text</c> choices added to the settings (a clear removes the choice). A back-cover choice for a book with no back cover is refused.</summary>
    private static KdpBookSettings ApplyText(KdpBookSettings settings, KdpExportRequest request, bool hasBack, Func<string, int> usedOffset)
    {
        foreach (var (key, text) in request.CoverText ?? new Dictionary<string, KdpCoverText?>())
        {
            if (key == KdpPictureKeys.Back && !hasBack && text is not null)
            {
                throw new KdpException("--text back names the back cover, but the book has none.");
            }

            settings = text is null ? settings.WithoutCoverText(key) : settings.WithCoverText(key, text with { ForOffsetPx = usedOffset(key) });
        }

        if (request.DinoText is { } dino)
        {
            if (dino.Colors is not null && !hasBack)
            {
                throw new KdpException("--dino-text names the Dino line beside the back cover's QR code, but the book has no back cover.");
            }

            settings = dino.Colors is null ? settings.WithoutDinoColors()
                : dino.Box is null ? settings.WithDinoColors(dino.Colors)
                : settings.WithDinoText(dino.Colors, dino.Box, usedOffset(KdpPictureKeys.Back));
        }

        return settings;
    }

    private static bool HasTextChanges(KdpExportRequest request) => request.CoverText is { Count: > 0 } || request.DinoText is not null;

    /// <summary>This run's <c>--speaker</c>, <c>--main-character</c> and <c>--title-picture</c> choices added to the settings (Slice 46); a choice the book cannot take is refused.</summary>
    private static KdpBookSettings ApplyStyles(KdpBookSettings settings, KdpExportRequest request, DinoBook book, DinoLanguage language, int storyPages)
    {
        return request.Styles is { IsEmpty: false } styles ? styles.ApplyTo(settings, book.CastIn(language.Code), storyPages) : settings;
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
    private static IEnumerable<string> StaleOffsetWarnings(KdpBookSettings settings, int storyPages, bool hasBack)
    {
        if (!hasBack && settings.Covers.Contains(KdpPictureKeys.Back))
        {
            yield return $"{KdpBookSettings.FileName} has choices for the back cover (offset, text), but the book has no back cover picture; they are ignored (--offset back=0 and --text back=clear remove them).";
        }

        foreach (var page in settings.Pages.Where(p => p > storyPages))
        {
            yield return $"{KdpBookSettings.FileName} has an offset for story page {page}, but the book has {storyPages} story pages; it is ignored.";
        }
    }

    /// <summary>
    /// Works out what this run's <c>--offset</c> choices make of <c>kdp.json</c>, in memory: only offsets that moved something (the value actually used) and
    /// clears, with the book's identity. Null when nothing changes. An offset with no effect is not saved; <c>KdpInterior</c> already warns about it once.
    /// </summary>
    private static KdpBookSettings? OffsetsToSave(KdpExportRequest request, DinoBook book, DinoLanguage language, IReadOnlyList<KdpPicture> pictures, KdpCoverResult? cover)
    {
        if (request.Offsets is not { Count: > 0 } && request.CoverOffsets is not { Count: > 0 } && !HasTextChanges(request) && request.Styles is not { IsEmpty: false })
        {
            return null;
        }

        try
        {
            var before = ReadStored(request, book);
            var settings = before;
            foreach (var (page, pixels) in request.Offsets ?? new Dictionary<int, int>())
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

            settings = CoverOffsetsToSave(request, settings, cover);
            settings = cover is null ? settings : ApplyText(settings, request, cover.BackPictureRead, key => AppliedCoverOffset(key, settings, cover));
            settings = ApplyStyles(settings, request, book, language, pictures.Count);
            return settings.ToJson() == before.ToJson() ? null : settings.ForBook(book.Id, book.Title);
        }
        catch (KdpSettingsException ex)
        {
            throw new KdpException(ex.Message, ex);
        }
    }

    /// <summary>A cover that could not be made saves no cover offset (its clamp is not known); say so, so the choice is not dropped silently.</summary>
    internal static IEnumerable<string> UnsavedCoverOffsetWarnings(KdpExportRequest request, KdpCoverResult? cover)
    {
        if (cover is not null)
        {
            yield break;
        }

        if (HasTextChanges(request))
        {
            yield return $"--text and --dino-text were not saved to {KdpBookSettings.FileName}, because the cover could not be made; run again once it can.";
        }

        foreach (var (key, pixels) in request.CoverOffsets ?? new Dictionary<string, int>())
        {
            if (pixels != 0)
            {
                yield return $"--offset {key}={pixels} was not saved to {KdpBookSettings.FileName}, because the cover could not be made; run again once it can.";
            }
        }
    }

    /// <summary>The cover offsets of this run that moved something (the value actually used) and the clears; a cover that could not be made saves none, since its clamp is not known.</summary>
    private static KdpBookSettings CoverOffsetsToSave(KdpExportRequest request, KdpBookSettings settings, KdpCoverResult? cover)
    {
        foreach (var (key, pixels) in request.CoverOffsets ?? new Dictionary<string, int>())
        {
            var picture = key == KdpPictureKeys.Front ? cover?.FrontPrepared : cover?.BackPrepared;
            if (pixels == 0)
            {
                settings = settings.WithoutCover(key);
            }
            else if (picture is { MaxOffsetPx: > 0 })
            {
                settings = settings.WithCoverOffset(key, picture.AppliedOffsetPx);
            }
        }

        return settings;
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

    /// <summary>
    /// The PDFs and the report go to temporary names first and are moved into place together, so a failed write never leaves a new PDF beside an old report; a cleanup failure never hides the first error.
    /// With no new cover (<paramref name="noCoverPath"/> set), an older cover.pdf is renamed once the PDFs are written, and the outcome goes into the report.
    /// </summary>
    private static StaleCover WriteAll(IReadOnlyList<(string Path, byte[] Bytes)> pdfs, string reportPath, string? noCoverPath, Func<StaleCover, string> buildReport)
    {
        var temps = pdfs.Select(p => p.Path + ".tmp").Append(reportPath + ".tmp").ToList();
        try
        {
            for (var i = 0; i < pdfs.Count; i++)
            {
                File.WriteAllBytes(temps[i], pdfs[i].Bytes);
            }

            var older = noCoverPath is null ? StaleCover.NotApplicable : KeepOlderCover(noCoverPath);
            File.WriteAllText(temps[^1], buildReport(older));
            for (var i = 0; i < pdfs.Count; i++)
            {
                File.Move(temps[i], pdfs[i].Path, overwrite: true);
            }

            File.Move(temps[^1], reportPath, overwrite: true);
            return older;
        }
        finally
        {
            temps.ForEach(TryDelete);
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
    internal static BookPictures ReadPictures(string path, DinoBook book)
    {
        var folder = Path.Combine(Path.GetTempPath(), "ptdb-kdp-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var stream = File.OpenRead(path);
            var extracted = DinoBookReader.ExtractPictures(stream, book, folder);
            byte[]? Read(string? file) => file is null ? null : File.ReadAllBytes(Path.Combine(folder, file));
            return new BookPictures(extracted.PageFiles.Select(file => Read(file)!).ToList(), Read(extracted.CoverFile), Read(extracted.BackCoverFile));
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
