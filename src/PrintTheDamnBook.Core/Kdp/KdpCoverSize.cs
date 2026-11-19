using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>
/// The KDP paperback cover (Slice 29): back, spine and front in one wrap. All lengths are inches. Every constant cites the KDP help page it came
/// from, read 2026-11-19. Cover width = bleed + back + spine + front + bleed; cover height = bleed + trim height + bleed
/// ("Create a Paperback Cover", https://kdp.amazon.com/en_US/help/topic/G201953020).
/// </summary>
public static class KdpCoverSize
{
    /// <summary>"Premium Color paper: page count x 0.002347" (G201953020, 2026-11-19).</summary>
    public const double SpineInchesPerPage = 0.002347;

    /// <summary>"add 0.125 in to the top, bottom, and outside edges of your cover" (G201953020, 2026-11-19).</summary>
    public const double BleedInches = 0.125;

    /// <summary>The barcode box KDP suggests: 2 x 1.2 in; the minimum is 1.4 x 0.8 in ("Barcodes", https://kdp.amazon.com/en_US/help/topic/G5HDYGP4BXLX4RUW, 2026-11-19).</summary>
    public const double BarcodeWidthInches = 2;

    public const double BarcodeHeightInches = 1.2;

    /// <summary>The barcode sits at least 0.25 in from the spine and the trim (same Barcodes page); 0.375 in (the interior's bleed-side safe margin) is used from the trim edges.</summary>
    public const double BarcodeMarginInches = 0.375;

    /// <summary>"minimum page count of 79 pages to include text on the spine" (G201953020, 2026-11-19; the submission guidelines G201857950 say "more than 79"). This book has 24 to 40 pages, so it never gets spine text.</summary>
    public const int SpineTextMinPages = 79;

    public const int PixelsPerInch = 300;

    public static double SpineWidth(int pageCount) => pageCount * SpineInchesPerPage;

    public static double CoverWidth(int pageCount) => 2 * BleedInches + 2 * KdpTrim.TrimInches + SpineWidth(pageCount);

    public static double CoverHeight => 2 * BleedInches + KdpTrim.TrimInches;

    public static bool HasSpineText(int pageCount) => pageCount >= SpineTextMinPages;

    private static bool IsRtl(ReadingDirection direction) => direction == ReadingDirection.RightToLeft;

    /// <summary>The panel that sits on the left of the file: the back cover of a left-to-right book, the front of a right-to-left one (bleed included on the outside edge).</summary>
    private static RectD LeftPanel => new(0, 0, BleedInches + KdpTrim.TrimInches, CoverHeight);

    /// <summary>The spine between the back and front panels.</summary>
    public static RectD Spine(int pageCount) => new(LeftPanel.Width, 0, SpineWidth(pageCount), CoverHeight);

    private static RectD RightPanel(int pageCount) => new(LeftPanel.Width + SpineWidth(pageCount), 0, KdpTrim.TrimInches + BleedInches, CoverHeight);

    /// <summary>The back cover: on the left for left-to-right books, on the right for right-to-left ones.</summary>
    public static RectD BackPanel(int pageCount, ReadingDirection direction) => IsRtl(direction) ? RightPanel(pageCount) : LeftPanel;

    /// <summary>The front cover: on the right for left-to-right books, on the left for right-to-left ones.</summary>
    public static RectD FrontPanel(int pageCount, ReadingDirection direction) => IsRtl(direction) ? LeftPanel : RightPanel(pageCount);

    /// <summary>The trim box of the back cover: inside the bleed, which is on its outside edge.</summary>
    public static RectD BackTrim(int pageCount, ReadingDirection direction)
    {
        var back = BackPanel(pageCount, direction);
        return new RectD(IsRtl(direction) ? back.X : BleedInches, BleedInches, KdpTrim.TrimInches, KdpTrim.TrimInches);
    }

    /// <summary>
    /// The barcode box, lower right of the back cover; for a right-to-left book KDP's "Barcodes" page puts it lower left of the back cover
    /// (https://kdp.amazon.com/en_US/help/topic/G5HDYGP4BXLX4RUW, 2026-11-19).
    /// </summary>
    public static RectD Barcode(int pageCount, ReadingDirection direction)
    {
        var trim = BackTrim(pageCount, direction);
        var x = IsRtl(direction) ? trim.X + BarcodeMarginInches : trim.X + trim.Width - BarcodeMarginInches - BarcodeWidthInches;
        return new RectD(x, trim.Y + trim.Height - BarcodeMarginInches - BarcodeHeightInches, BarcodeWidthInches, BarcodeHeightInches);
    }

    /// <summary>The pixel size of a picture that fills a panel at 300 dpi.</summary>
    public static (int Width, int Height) PanelPixels(RectD panel) =>
        ((int)Math.Round(panel.Width * PixelsPerInch, MidpointRounding.AwayFromZero), (int)Math.Round(panel.Height * PixelsPerInch, MidpointRounding.AwayFromZero));
}
