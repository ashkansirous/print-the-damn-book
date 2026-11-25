using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Layout;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PrintTheDamnBook.Kdp.Styling;

/// <summary>
/// The only drawing of a <see cref="StyledPage"/> (Slice 46): one QuestPDF text block, centred, the page line height, paragraphs
/// <see cref="StyleSpec.ParagraphGapPt"/> apart (R1), every run a span with its own size, weight, slant, colour and letter spacing.
/// The page's drawing and <see cref="KdpTextFit"/>'s probe both call it, so they wrap alike.
/// </summary>
internal static class KdpStyledText
{
    public static void Words(IContainer body, StyledPage page, string family, bool rightToLeft)
    {
        (rightToLeft ? body.ContentFromRightToLeft() : body).Text(t =>
        {
            t.AlignCenter();
            t.ParagraphSpacing((float)StyleSpec.ParagraphGapPt);
            t.DefaultTextStyle(s => s.FontFamily(family).FontSize((float)RunLook.Body.Pt).LineHeight((float)StoryTextLayout.PageLineHeight).FontColor(KdpInteriorDocument.Ink));
            for (var p = 0; p < page.Paragraphs.Count; p++)
            {
                Paragraph(t, page.Paragraphs[p], p + 1 < page.Paragraphs.Count, family, rightToLeft);
            }
        });
    }

    private static void Paragraph(TextDescriptor t, StyledParagraph paragraph, bool more, string family, bool rightToLeft)
    {
        foreach (var run in paragraph.Runs)
        {
            Run(t, run, family, rightToLeft);
        }

        if (more)
        {
            t.Span("\n");
        }
    }

    /// <summary>One run as a span; a run that needs an inline element (a baseline move, a turn or a song word: R6, R10) is drawn by <see cref="KdpInlineWord"/>.</summary>
    private static void Run(TextDescriptor t, StyledRun run, string family, bool rightToLeft)
    {
        var look = run.Look;
        if (look.NeedsInlineElement)
        {
            KdpInlineWord.Draw(t, run, family, rightToLeft);
            return;
        }

        var span = t.Span(run.Text).FontSize((float)look.Pt).FontColor(look.Color);
        if (look.Bold)
        {
            span.Bold();
        }

        if (look.Italic)
        {
            span.Italic();
        }

        if (look.LetterSpacingEm != 0)
        {
            span.LetterSpacing((float)look.LetterSpacingEm);
        }
    }
}
