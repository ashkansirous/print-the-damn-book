using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>
/// The Amazon KDP paperback interior (Slice 27): 8.5 x 8.5 in trim, premium colour, with bleed. KDP's page with bleed is the trim
/// plus 0.125 in on the outside edge and 0.125 in above and below, so every page is 8.625 x 8.75 in; the picture fills all of it.
/// All lengths are inches unless the name says otherwise.
/// </summary>
public static class KdpTrim
{
    public const double TrimInches = 8.5;

    /// <summary>KDP adds 0.125 in to the width (outside edge only) and 0.25 in to the height (0.125 in above and below).</summary>
    public const double BleedWidthInches = 0.125;

    public const double BleedHeightInches = 0.25;

    public const double PageWidthInches = TrimInches + BleedWidthInches;

    public const double PageHeightInches = TrimInches + BleedHeightInches;

    public const int PixelsPerInch = 300;

    /// <summary>8.625 in at 300 dpi.</summary>
    public const int PictureWidthPx = 2588;

    /// <summary>8.75 in at 300 dpi.</summary>
    public const int PictureHeightPx = 2625;

    /// <summary>
    /// Nothing but the picture comes closer to the trim edge than this. Source, read 2026-11-19: "Set Trim Size, Bleed, and Margins",
    /// https://kdp.amazon.com/en_US/help/topic/GVBQ3CMEQW3W2VL6 : for 24 to 150 pages the inside (gutter) margin is at least 0.375 in, and the
    /// outside margin at least 0.25 in without bleed and at least 0.375 in with bleed (this book has bleed). 0.5 in is used on every side.
    /// </summary>
    public const double SafeMarginInches = 0.5;

    /// <summary>The story words keep this far from the trim edge on every side.</summary>
    public const double TextInsetInches = 0.85;

    public const double NumberBoxHeightInches = 0.3;

    public const double NumberPt = 11;

    public const double PointsPerInch = 72;

    /// <summary>The words' sizes, largest first (the KDP twin of <see cref="StoryTextLayout.PageStepsPt"/>, which knows only A4 and Letter).</summary>
    public static readonly IReadOnlyList<double> TextStepsPt = [24, 22, 20, 18, 16, 14];

    /// <summary>The title on the title page.</summary>
    public const double TitlePt = 36;

    /// <summary>The copyright and credit lines.</summary>
    public const double SmallPt = 11;

    /// <summary>The words of the dedication, the end page and the about page.</summary>
    public const double MatterPt = 20;

    /// <summary>The trim box on a page of the page's own size: the outside edge carries the bleed, so a left-hand page starts after it.</summary>
    public static RectD TrimRect(bool rightHandPage)
    {
        var x = rightHandPage ? 0 : BleedWidthInches;
        return new RectD(x, BleedHeightInches / 2, TrimInches, TrimInches);
    }

    /// <summary>The box the story words are set in.</summary>
    public static RectD TextBox(bool rightHandPage)
    {
        var trim = TrimRect(rightHandPage);
        return new RectD(trim.X + TextInsetInches, trim.Y + TextInsetInches, trim.Width - 2 * TextInsetInches, trim.Height - 2 * TextInsetInches);
    }

    /// <summary>The strip the page number sits in, bottom centre, inside the safe margin.</summary>
    public static RectD NumberBox(bool rightHandPage)
    {
        var trim = TrimRect(rightHandPage);
        var top = trim.Y + trim.Height - SafeMarginInches - NumberBoxHeightInches;
        return new RectD(trim.X + SafeMarginInches, top, trim.Width - 2 * SafeMarginInches, NumberBoxHeightInches);
    }

    public static double ToPoints(double inches) => inches * PointsPerInch;
}
