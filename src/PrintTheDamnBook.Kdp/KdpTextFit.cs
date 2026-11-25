using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PrintTheDamnBook.Kdp;

/// <summary>
/// Measures whether words fit their box, and sets them. Story text is always one size (<see cref="KdpTrim.StoryTextPt"/>, Slice 42) and its lines wrap; nothing here
/// picks or shrinks a size. QuestPDF has no measuring call, so the words are set in a one-page document of the box's size with
/// <c>ShowEntire</c>, which throws when they overflow; <see cref="Fits"/> then answers false and the build reports an error for that page. The drawing (<c>KdpInteriorDocument.StoryText</c>)
/// uses <c>StopPaging</c> instead, so an overflowing page is still written, with its words clipped at the box edge (they are centred vertically, so the clip can fall at the top and the bottom). Both
/// use the same page size, box, font, size and line height, so they wrap alike and "does not fit" in the probe means "runs past the box" in the drawing.
/// </summary>
internal static class KdpTextFit
{
    private const double ProbePt = 100;
    private const string ProbeText = "Ag";
    private const double ProbeMinPt = 50;
    private const double ProbeMaxPt = 400;
    private const int ProbeSteps = 14;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, double> LineHeightsAtProbe = new(StringComparer.Ordinal);

    public static bool Fits(string text, string family, double pt, RectD boxInches, bool rightToLeft)
    {
        return Fits(boxInches, [text], family, pt, rightToLeft);
    }

    /// <summary>The height the words really take: the smallest whole number of lines (plus a little slack) whose box they fit, at most <paramref name="maxHeightInches"/>.</summary>
    internal static double TightHeightInches(string text, string family, double pt, double widthInches, double maxHeightInches, bool rightToLeft)
    {
        double lineHeightPt;
        try
        {
            lineHeightPt = LineHeightPt(family, pt);
        }
        catch (InvalidOperationException)
        {
            return maxHeightInches;
        }

        var lineInches = lineHeightPt / KdpTrim.PointsPerInch;
        for (var lines = 1; lines * lineInches < maxHeightInches; lines++)
        {
            var height = lines * lineInches + TightSlackInches;
            if (Fits(new RectD(0, 0, widthInches, height), [text], family, pt, rightToLeft))
            {
                return height;
            }
        }

        return maxHeightInches;
    }

    /// <summary>The height of the lines the words really take at their wrapped line count, with no slack; null when the font probe fails or the words need more than <paramref name="maxHeightInches"/>.</summary>
    internal static double? WrappedLinesHeightInches(string text, string family, double pt, double widthInches, double maxHeightInches, bool rightToLeft)
    {
        double lineInches;
        try
        {
            lineInches = LineHeightPt(family, pt) / KdpTrim.PointsPerInch;
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        for (var lines = 1; lines * lineInches < maxHeightInches; lines++)
        {
            if (Fits(new RectD(0, 0, widthInches, lines * lineInches + TightSlackInches), [text], family, pt, rightToLeft))
            {
                return lines * lineInches;
            }
        }

        return null;
    }

    private const double TightSlackInches = 0.04;

    /// <summary>The height one line of <paramref name="pt"/> takes in the page's line height, measured once per font with the same engine (QuestPDF has no measuring call): the smallest box a probe line fits, scaled.</summary>
    internal static double LineHeightPt(string family, double pt)
    {
        var atProbe = LineHeightsAtProbe.GetOrAdd(family, MeasureProbeLine);
        return atProbe * pt / ProbePt;
    }

    /// <summary>Measures a font's line height; throws <see cref="InvalidOperationException"/> when even the largest probe box does not hold a line (a missing font), so a failed probe is never cached or taken for a measurement.</summary>
    private static double MeasureProbeLine(string family)
    {
        KdpFonts.EnsureRegistered();
        double low = ProbeMinPt;
        double high = ProbeMaxPt;
        var largest = new RectD(0, 0, KdpTrim.TrimInches, high / KdpTrim.PointsPerInch);
        var measurable = false;
        try
        {
            measurable = Fits(largest, [ProbeText], family, ProbePt, false);
        }
        catch (DocumentDrawingException)
        {
        }

        if (!measurable)
        {
            throw new InvalidOperationException($"The line height of font '{family}' could not be measured.");
        }

        for (var step = 0; step < ProbeSteps; step++)
        {
            var middle = (low + high) / 2;
            var box = new RectD(0, 0, KdpTrim.TrimInches, middle / KdpTrim.PointsPerInch);
            if (Fits(box, [ProbeText], family, ProbePt, false))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return high;
    }

    /// <summary>Each text is set in the box on a page of its own; all must fit.</summary>
    private static bool Fits(RectD boxInches, IReadOnlyList<string> texts, string family, double pt, bool rightToLeft)
    {
        var document = Document.Create(container =>
        {
            foreach (var text in texts)
            {
                container.Page(page =>
                {
                    page.Size((float)KdpTrim.PageWidthInches, (float)KdpTrim.PageHeightInches, Unit.Inch);
                    page.Margin(0);
                    page.Content().Element(body => KdpInteriorDocument.InBox(body, boxInches).AlignMiddle().ShowEntire().Element(words => Words(words, text, family, pt, rightToLeft)));
                });
            }
        });

        try
        {
            document.GeneratePdf(Stream.Null);
            return true;
        }
        catch (DocumentLayoutException)
        {
            return false;
        }
    }

    /// <summary>The words as the page sets them: centred, the story font and ink, the page line height.</summary>
    public static void Words(IContainer body, string text, string family, double pt, bool rightToLeft, string? color = null)
    {
        (rightToLeft ? body.ContentFromRightToLeft() : body).Text(t =>
        {
            t.AlignCenter();
            t.DefaultTextStyle(s => s.FontFamily(family).FontSize((float)pt).LineHeight((float)StoryTextLayout.PageLineHeight).FontColor(color ?? KdpInteriorDocument.Ink));
            t.Span(text);
        });
    }
}
