using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Kdp;

/// <summary>A book as the interior needs it: its words (already in the chosen language) and its encoded pictures, one pair per story page.</summary>
public sealed record KdpBookInput(
    string Title,
    string Language,
    DinoCredits? Credits,
    int Year,
    IReadOnlyList<string> Texts,
    IReadOnlyList<byte[]> Pictures);

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
        var pictures = book.Pictures.Select(KdpImageDecoder.Prepare).ToList();
        var warnings = new List<string>(plan.Warnings);
        var stories = ChooseSizes(book, plan, direction, warnings);
        warnings.AddRange(UpscaleWarnings(pictures));

        var digits = PageNumbering.DigitsFor(book.Language);
        var drawing = new KdpDrawing(
            plan,
            KdpFrontMatterWording.For(book.Title, book.Credits, book.Year),
            direction,
            stories.Select(s => new KdpStoryPage(book.Texts[s.StoryIndex], s.Pt)).ToList(),
            pictures,
            digits);
        var pdf = KdpInteriorDocument.Render(drawing);
        var fonts = KdpPdfInspector.CheckFonts(pdf);
        if (!fonts.AllEmbedded)
        {
            throw new KdpException($"{fonts.Fonts - fonts.Embedded} of {fonts.Fonts} fonts in the PDF are not embedded; KDP rejects that.");
        }

        return new KdpInteriorResult(pdf, plan, pictures, stories, fonts, warnings);
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
            var chosen = StoryTextLayout.Choose(KdpTrim.TextStepsPt, pt => KdpTextFit.Fits(text, family, pt, box, rtl));
            if (!chosen.Fits)
            {
                throw new KdpException($"The words of page {page.Position} do not fit their box even at {chosen.Pt} pt.");
            }

            if (chosen.Shrunk)
            {
                warnings.Add($"Page {page.Position}: the words were set at {chosen.Pt} pt (smaller than the first size, {KdpTrim.TextStepsPt[0]} pt).");
            }

            results.Add(new KdpStoryResult(page.StoryIndex.Value, page.Position, chosen.Pt, chosen.Shrunk));
        }

        return results.OrderBy(r => r.StoryIndex).ToList();
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
