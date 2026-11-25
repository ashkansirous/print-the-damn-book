namespace PrintTheDamnBook.Core.Kdp;

/// <summary>
/// The sizes, colours and spacing of the front and back pages of the KDP interior (Slice 46 item G), all from <c>docs/design/kdp-typography.md</c> (the ★ mocks
/// <c>Matter.dc.html</c> and <c>DinoLink.dc.html</c>), 1 px of the mock = 1 pt. The Kdp project only draws them.
/// </summary>
public static class KdpMatterSpec
{
    public const string MutedInk = "#6E6880";
    public const string White = "#FFFFFF";
    public const double LineHeight = 1.35;

    /// <summary>The trim size in points (the mock's 612 pt square page).</summary>
    public static double TrimPt => KdpTrim.TrimInches * KdpTrim.PointsPerInch;

    // Credits page
    public const double CreditsBottomPt = 61;
    public const double CreditsGroupGapPt = 18;
    public const double CreditsLabelPt = 13;
    public const double CreditsNamePt = 20;
    public const double CreditsLabelGapPt = 2;
    public const double CreditsSmallPt = 12;
    public const double CreditsSmallLineHeight = 1.5;

    // Belongs-to page
    public const string FrameColor = "#8A4B12";
    public const double FrameInsetPt = 36;
    public const double FrameBorderPt = 3;
    public const double FrameRadiusPt = 20;
    public const double FramePaddingPt = 32;
    public const double BelongsItemGapPt = 22;
    public const double BelongsHeadingPt = 28;
    public const double NameLineShare = 0.85;
    public const double NameLineBorderPt = 3;
    public const double NameLineHeightPt = 36;
    public const double DrawBoxWidthPt = 220;
    public const double DrawBoxHeightPt = 190;
    public const double DrawBoxBorderPt = 2;
    public const double DrawBoxRadiusPt = 14;
    public const double DrawLabelPt = 15;
    public const double DrawLabelBottomPt = 12;

    // The End
    public const double TheEndPt = 72;

    // Dino page
    public const double DinoSidePaddingPt = 61;
    public const double DinoItemGapPt = 18;
    public const double QrPt = 150;
    public const double ScanPt = 14;
    public const double AddressPt = 26;
    public const double AddressLetterSpacingEm = 0.02;
    public const double StoresPt = 13;
    public const double AddressBlockGapPt = 2;
    public const double ListenPt = 20;
    public const double JoinPt = 24;
    public const double NamesPt = 20;
    public const double NameRowGapPt = 6;
    public const double NameColumnGapPt = 16;

    /// <summary>The width of the content inside the belongs-to frame (trim, minus both insets, both borders and both paddings).</summary>
    public static double FrameInnerWidthPt => TrimPt - 2 * FrameInsetPt - 2 * FrameBorderPt - 2 * FramePaddingPt;

    /// <summary>The width of the dotted name line.</summary>
    public static double NameLineWidthPt => FrameInnerWidthPt * NameLineShare;
}
