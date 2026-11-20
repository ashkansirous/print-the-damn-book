using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;
using QuestPDF.Fluent;

namespace PrintTheDamnBook.Kdp;

/// <summary>A book as the interior needs it: its words (already in the chosen language) and its encoded pictures, one pair per story page.</summary>
public sealed record KdpBookInput(
    string Title,
    string Language,
    DinoCredits? Credits,
    int Year,
    IReadOnlyList<string> Texts,
    IReadOnlyList<byte[]> Pictures,
    KdpBookSettings? Settings = null);

/// <summary>The words of one story page and what was chosen for them.</summary>
public sealed record KdpStoryResult(int StoryIndex, int Position, double Pt, bool Shrunk);

/// <summary>What was made: the PDF and the facts the check report lists.</summary>
public sealed record KdpInteriorResult(
    byte[] Pdf,
    KdpPlan Plan,
    IReadOnlyList<KdpPicture> Pictures,
    IReadOnlyList<KdpStoryResult> Stories,
    KdpFontCheck Fonts,
    IReadOnlyList<string> Warnings);

/// <summary>Makes the KDP interior PDF of a book: plan, pictures, text sizes, drawing and the checks on the result (Slice 27).</summary>
public static class KdpInterior
{
    public static KdpInteriorResult Build(KdpBookInput book)
    {
        if (book.Texts.Count != book.Pictures.Count || book.Texts.Count == 0)
        {
            throw new KdpException("A book needs the same number of texts and pictures, and at least one.");
        }

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        KdpFonts.EnsureRegistered();
        var direction = TextDirection.ForLanguage(book.Language);
        var plan = KdpPagePlan.Build(book.Texts.Count, direction);
        var settings = book.Settings ?? KdpBookSettings.Empty;
        var pictures = book.Pictures.Select((bytes, i) => KdpImageDecoder.Prepare(bytes, settings.OffsetFor(i + 1), settings.HasOffset(i + 1))).ToList();
        var warnings = new List<string>(plan.Warnings);
        var stories = ChooseSizes(book, plan, direction, warnings);
        warnings.AddRange(UpscaleWarnings(pictures));
        warnings.AddRange(CutWarnings(pictures));
        warnings.AddRange(OffsetWarnings(pictures));

        var drawing = DrawingFor(book, plan, pictures, stories);
        var pdf = KdpInteriorDocument.CreateDocument(drawing).GeneratePdf();
        var fonts = KdpPdfInspector.CheckFonts(pdf);
        if (!fonts.AllEmbedded)
        {
            throw new KdpException($"{fonts.Fonts - fonts.Embedded} of {fonts.Fonts} fonts in the PDF are not embedded; KDP rejects that.");
        }

        return new KdpInteriorResult(pdf, plan, pictures, stories, fonts, warnings);
    }

    /// <summary>The one place the interior's drawing is put together; the PDF and the <c>--previews</c> pages (Slice 30) both come from it, so they cannot differ.</summary>
    internal static KdpDrawing DrawingFor(KdpBookInput book, KdpPlan plan, IReadOnlyList<KdpPicture> pictures, IReadOnlyList<KdpStoryResult> stories)
    {
        return new KdpDrawing(
            plan,
            KdpFrontMatterWording.For(book.Title, book.Credits, book.Year),
            TextDirection.ForLanguage(book.Language),
            stories.Select(s => new KdpStoryPage(book.Texts[s.StoryIndex], s.Pt, pictures[s.StoryIndex].Tint)).ToList(),
            pictures,
            PageNumbering.DigitsFor(book.Language));
    }

    private static List<KdpStoryResult> ChooseSizes(KdpBookInput book, KdpPlan plan, ReadingDirection direction, List<string> warnings)
    {
        var results = new List<KdpStoryResult>();
        foreach (var page in plan.Pages.Where(p => p.Role == KdpPageRole.StoryText))
        {
            var text = book.Texts[page.StoryIndex!.Value];
            var box = KdpTrim.TextBox(page.IsRightHand);
            var family = StoryFont.For(text);
            var rtl = direction == ReadingDirection.RightToLeft;
            var chosen = ChooseSize(text, family, box, rtl, page.Position, warnings);
            if (chosen.Shrunk)
            {
                warnings.Add($"Page {page.Position}: the words were set at {chosen.Pt} pt (smaller than the first size, {KdpTrim.TextStepsPt[0]} pt).");
            }

            results.Add(new KdpStoryResult(page.StoryIndex.Value, page.Position, chosen.Pt, chosen.Shrunk));
        }

        return results.OrderBy(r => r.StoryIndex).ToList();
    }

    /// <summary>
    /// The largest size at which every one of the book's own lines stays on one line (Slice 36). When no size does, even the smallest, the words are set at
    /// the largest size that fits their box with wrapping and the page is reported; words that fit at none stop the build.
    /// </summary>
    private static TextFitResult ChooseSize(string text, string family, RectD box, bool rtl, int position, List<string> warnings)
    {
        var whole = StoryTextLayout.Choose(KdpTrim.TextStepsPt, pt => KdpTextFit.LinesStayWhole(text, family, pt, box, rtl));
        if (whole.Fits)
        {
            return whole;
        }

        var wrapped = StoryTextLayout.Choose(KdpTrim.TextStepsPt, pt => KdpTextFit.Fits(text, family, pt, box, rtl));
        if (!wrapped.Fits)
        {
            throw new KdpException($"The words of page {position} do not fit their box even at {wrapped.Pt} pt.");
        }

        warnings.Add($"Page {position}: a line of the words does not fit on one line even at {KdpTrim.TextStepsPt[^1]} pt, so it wraps (set at {wrapped.Pt} pt).");
        return wrapped;
    }

    /// <summary>One line for the pages where art likely runs across the cut line, with the way to move the crop.</summary>
    private static IEnumerable<string> CutWarnings(IReadOnlyList<KdpPicture> pictures)
    {
        var pages = Enumerable.Range(1, pictures.Count).Where(n => pictures[n - 1].Flagged).ToList();
        if (pages.Count > 0)
        {
            yield return $"{pages.Count} picture(s) may be cut through a character: story page(s) {string.Join(", ", pages)}. Look at the previews, then move the crop with --offset <page>=<pixels>.";
        }
    }

    /// <summary>A line for each page whose chosen offset could not be applied as asked (no spare height, or more than the picture allows).</summary>
    private static IEnumerable<string> OffsetWarnings(IReadOnlyList<KdpPicture> pictures)
    {
        for (var i = 0; i < pictures.Count; i++)
        {
            if (OffsetWarning($"Story page {i + 1}", pictures[i]) is { } warning)
            {
                yield return warning;
            }
        }
    }

    /// <summary>The line for a picture whose chosen offset could not be applied as asked; null when it was. Shared with the cover pictures.</summary>
    internal static string? OffsetWarning(string name, KdpPicture p)
    {
        if (p.OffsetIgnored)
        {
            return $"{name}: the offset of {p.RequestedOffsetPx} px has no effect, because this picture has no spare height to move the crop (it is cropped at the sides, not the bottom). An --offset for it is not saved.";
        }

        return p.OffsetClamped ? $"{name}: the offset of {p.RequestedOffsetPx} px is more than the picture allows; {p.AppliedOffsetPx} px was used." : null;
    }

    /// <summary>One line for all the pictures that are enlarged: how many, and the lowest resolution they end up with on the page.</summary>
    private static IEnumerable<string> UpscaleWarnings(IReadOnlyList<KdpPicture> pictures)
    {
        var small = pictures.Where(p => p.Crop.Upscales).ToList();
        if (small.Count == 0)
        {
            yield break;
        }

        var lowest = small.MinBy(p => p.Crop.EffectiveDpi)!;
        yield return $"{small.Count} of {pictures.Count} pictures are enlarged to {lowest.Crop.TargetWidth} px across; the lowest has {lowest.Crop.Source.Width} px, about {lowest.Crop.EffectiveDpi:0} dpi on the page (KDP asks for 300).";
    }
}
