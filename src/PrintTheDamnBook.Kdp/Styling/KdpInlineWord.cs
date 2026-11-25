using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Layout;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PrintTheDamnBook.Kdp.Styling;

/// <summary>
/// The words of a story page that a plain span cannot draw (Slice 46 items E and F): a word moved along the baseline (R6 down, up, bounce), turned (R6 shake) or bouncing
/// in a song (R10). QuestPDF cannot shift or turn one span, so each such run is an injected inline element holding the word, padded so its line grows by the move and no
/// neighbouring line is touched. The shapes and calibration come from the spike of item A (<c>KdpStyledTextSpikeTests</c>): QuestPDF 2026.9.2 names the shift <c>OffsetY</c>;
/// <c>Rotate</c> turns about the element's centre without changing the layout; an injected word stays real text in the PDF.
/// </summary>
internal static class KdpInlineWord
{
    // An injected word sits against the line edge it is aligned to, not on the baseline of the words around it. Measured at 24 pt in Andika with 24 pt neighbours only
    // (the difference does not scale with the size): aligned to the bottom (inner line height 1.35) it sits 0.032 em above their baseline, aligned to the top (inner line
    // height 1) it sits 0.203 em below it. OffsetY (it moves the drawing, not the layout) takes that difference out. Shifts apply only to words of 24 pt lines.
    private const float BottomEm = 0.032f;
    private const float TopEm = 0.203f;
    private const float TopLineHeight = 1f;

    public static void Draw(TextDescriptor t, StyledRun run, string family, bool rightToLeft)
    {
        var look = run.Look;
        if (look.Song)
        {
            Bouncing(t, run, family, rightToLeft);
        }
        else if (look.RotationDeg != 0 || look.SideMarginPt != 0)
        {
            Turned(t, run, family, rightToLeft);
        }
        else
        {
            Shifted(t, run, family, rightToLeft);
        }
    }

    /// <summary>A word set <see cref="RunLook.BaselinePt"/> above (positive) or below (negative) the baseline of its line; the line grows by that much on that side.</summary>
    private static void Shifted(TextDescriptor t, StyledRun run, string family, bool rightToLeft)
    {
        var look = run.Look;
        var up = (float)look.BaselinePt;
        if (up >= 0)
        {
            t.Element(e => Direction(e, rightToLeft).PaddingBottom(up).OffsetY(BottomEm * (float)look.Pt).Text(w => Styled(w.Span(run.Text), look, family, (float)StoryTextLayout.PageLineHeight)), TextInjectedElementAlignment.Bottom);
        }
        else
        {
            t.Element(e => Direction(e, rightToLeft).PaddingTop(-up).OffsetY(-TopEm * (float)look.Pt).Text(w => Styled(w.Span(run.Text), look, family, TopLineHeight)), TextInjectedElementAlignment.Top);
        }
    }

    /// <summary>A song word (R10): every word of the line is aligned to the bottom, and a high word gets twice the bounce as bottom padding, so high words sit exactly that far above low ones whatever the size, with nothing to calibrate.</summary>
    private static void Bouncing(TextDescriptor t, StyledRun run, string family, bool rightToLeft)
    {
        var look = run.Look;
        var padding = look.BaselinePt > 0 ? (float)(2 * StyleSpec.SongBouncePt) : 0;
        t.Element(e => Direction(e, rightToLeft).PaddingBottom(padding).Text(w => Styled(w.Span(run.Text), look, family, (float)StoryTextLayout.PageLineHeight)), TextInjectedElementAlignment.Bottom);
    }

    /// <summary>A turned word: the box grows by the extra height a turn needs on each side (maths in Core), and by the side margin each side.</summary>
    private static void Turned(TextDescriptor t, StyledRun run, string family, bool rightToLeft)
    {
        var look = run.Look;
        var extra = (float)ShapeMaths.PaddingFor(look, run.Text, StoryTextLayout.PageLineHeight);
        t.Element(
            e => Direction(e, rightToLeft).PaddingHorizontal((float)look.SideMarginPt).PaddingVertical(extra).OffsetY(BottomEm * (float)look.Pt).Rotate((float)look.RotationDeg).Text(w => Styled(w.Span(run.Text), look, family, (float)StoryTextLayout.PageLineHeight)),
            TextInjectedElementAlignment.Bottom);
    }

    private static IContainer Direction(IContainer e, bool rightToLeft) => rightToLeft ? e.ContentFromRightToLeft() : e;

    /// <summary>The span's own style: an injected element does not take the text block's default style, so family, size and colour are all set here.</summary>
    private static void Styled(TextSpanDescriptor span, RunLook look, string family, float lineHeight)
    {
        span.FontFamily(family).FontSize((float)look.Pt).FontColor(look.Color).LineHeight(lineHeight);
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
