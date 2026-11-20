namespace PrintTheDamnBook.Core.Kdp;

/// <summary>The words the cover adds around the pictures (Slice 36); the Kdp project only draws them. The front carries the title only (Slice 37); the credits are on the back.</summary>
public static class KdpCoverWording
{
    /// <summary>The page the back cover's and the last interior page's QR code opens.</summary>
    public const string DinoReaderUrl = "https://dinoreader.sirous.uk/";

    /// <summary>The line next to the QR code, on the back cover and on the last interior page (one text for both, <see cref="KdpFrontMatterWording.DinoLine"/>).</summary>
    public static string DinoLine => KdpFrontMatterWording.DinoLine;
}
