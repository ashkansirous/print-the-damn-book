using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp.Styling;
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
    KdpBookSettings? Settings = null,
    IReadOnlyList<byte[]>? OtherCovers = null,
    IReadOnlyList<string>? Cast = null);

/// <summary>The words of one story page and what was chosen for them.</summary>
public sealed record KdpStoryResult(int StoryIndex, int Position, double Pt, bool Fits = true, StyledPage? Styled = null);

/// <summary>What was made: the PDF and the facts the check report lists.</summary>
public sealed record KdpInteriorResult(
    byte[] Pdf,
    KdpPlan Plan,
    IReadOnlyList<KdpPicture> Pictures,
    IReadOnlyList<KdpStoryResult> Stories,
    KdpFontCheck Fonts,
    IReadOnlyList<string> Warnings,
    KdpStyleOutcome? Style = null)
{
    /// <summary>One line for each story text page whose words do not fit their box at the one text size. The build still writes everything, and the command exits non-zero.</summary>
    public IReadOnlyList<string> Errors => Stories.Where(s => !s.Fits)
        .Select(s => $"Page {s.Position}: the words do not fit their box at {KdpTrim.StoryTextPt} pt; the text runs past its box and is cut off at its edge.")
        .ToList();
}

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
        var plan = KdpPagePlan.Build(book.Texts.Count, direction, moreBooks: book.OtherCovers is { Count: > 0 });
        var settings = book.Settings ?? KdpBookSettings.Empty;
        var pictures = book.Pictures.Select((bytes, i) => KdpImageDecoder.Prepare(bytes, settings.OffsetFor(i + 1), settings.HasOffset(i + 1))).ToList();
        var warnings = new List<string>(plan.Warnings);
        var style = KdpStyleSetup.Prepare(book, pictures, settings, warnings);
        warnings.AddRange(TitlePictureWarnings(settings, book.Texts.Count));
        var stories = ChooseSizes(book, plan, direction, pictures, style);
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

        return new KdpInteriorResult(pdf, plan, pictures, stories, fonts, warnings, style);
    }

    /// <summary>The one place the interior's drawing is put together; the PDF and the <c>--previews</c> pages (Slice 30) both come from it, so they cannot differ.</summary>
    internal static KdpDrawing DrawingFor(KdpBookInput book, KdpPlan plan, IReadOnlyList<KdpPicture> pictures, IReadOnlyList<KdpStoryResult> stories)
    {
        var front = FrontPagesFor(book, plan, pictures);
        return new KdpDrawing(
            plan,
            KdpFrontMatterWording.For(book.Title, book.Credits, book.Year),
            TextDirection.ForLanguage(book.Language),
            stories.Select(s => new KdpStoryPage(s.Styled ?? StyledPage.Plain(book.Texts[s.StoryIndex]), s.Pt, pictures[s.StoryIndex].Tint)).ToList(),
            pictures,
            PageNumbering.DigitsFor(book.Language),
            book.OtherCovers ?? [],
            front);
    }

    /// <summary>
    /// The title page's picture and words, the tints of the title page and The End (those of the first and of the last story text page), The End's colour (the main character's,
    /// else ink) and the speaking characters of the Dino page, from the same style choices the story pages used (Slice 46 item G).
    /// </summary>
    private static KdpFrontPages FrontPagesFor(KdpBookInput book, KdpPlan plan, IReadOnlyList<KdpPicture> pictures)
    {
        var settings = book.Settings ?? KdpBookSettings.Empty;
        var style = KdpStyleSetup.Prepare(book, pictures, settings, []);
        var textPages = plan.Pages.Where(p => p.Role == KdpPageRole.StoryText).ToList();
        var titleTint = pictures[textPages[0].StoryIndex!.Value].Tint;
        var endTint = pictures[textPages[^1].StoryIndex!.Value].Tint;
        var styler = new StoryStyler(book.Texts, book.Language, style.Voices, style.SpeakerColours);
        var title = TitleLines.Split(styler.StyleTitle(book.Title, titleTint));
        var saved = settings.Styles.TitlePicture;
        var circle = KdpImageDecoder.TitleSquare(book.Pictures[KdpTitleCircle.PageFor(saved, book.Pictures.Count) - 1], saved, book.Pictures.Count, TitleCirclePx);
        var endColor = style.MainCharacter is { } main && style.SpeakerColours.TryGetValue(main, out var hex) ? hex : KdpInteriorDocument.Ink;
        var speakers = style.Order.Where(style.SpeakerColours.ContainsKey).Select(name => new KdpSpeakerName(name, style.SpeakerColours[name])).ToList();
        return new KdpFrontPages(circle, title, titleTint, endTint, endColor, speakers);
    }

    /// <summary>The round title picture is cut at 300 dpi for its 230 pt.</summary>
    private const int TitleCirclePx = 960;

    /// <summary>No saved title picture, or one for a page the book does not have: the first story picture, centred, is used (decision 12), and the report says so.</summary>
    private static IEnumerable<string> TitlePictureWarnings(KdpBookSettings settings, int pictureCount)
    {
        var saved = settings.Styles.TitlePicture;
        if (saved is null)
        {
            yield return "styles: no saved title picture (fallback): the first story picture, centred; the kdp-style skill chooses one with --title-picture <page>=<cx>,<cy>,<d>.";
        }
        else if (saved.Page > pictureCount)
        {
            yield return $"styles: the saved title picture is for story page {saved.Page}, which this book does not have (fallback): the first story picture, centred.";
        }
    }

    /// <summary>Every story text page is set at <see cref="KdpTrim.StoryTextPt"/> and its lines wrap (Slice 42); a page whose words do not fit its box at that size is reported as an error, never shrunk.</summary>
    private static List<KdpStoryResult> ChooseSizes(KdpBookInput book, KdpPlan plan, ReadingDirection direction, IReadOnlyList<KdpPicture> pictures, KdpStyleOutcome style)
    {
        var rtl = direction == ReadingDirection.RightToLeft;
        var styler = new StoryStyler(book.Texts, book.Language, style.Voices, style.SpeakerColours);
        var results = new List<KdpStoryResult>();
        foreach (var page in plan.Pages.Where(p => p.Role == KdpPageRole.StoryText))
        {
            var index = page.StoryIndex!.Value;
            var styled = styler.Style(index, pictures[index].Tint);
            var fits = KdpTextFit.Fits(styled, StoryFont.For(styled.Text), KdpTrim.TextBox(page.IsRightHand), rtl);
            results.Add(new KdpStoryResult(index, page.Position, KdpTrim.StoryTextPt, fits, styled));
        }

        return results.OrderBy(r => r.StoryIndex).ToList();
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
