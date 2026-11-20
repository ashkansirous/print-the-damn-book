using QRCoder;

namespace PrintTheDamnBook.Kdp.Cover;

/// <summary>
/// The QR code on the back cover (Slice 36): dark modules on white with the 4-module quiet zone the standard asks for, as a PNG (QRCoder's
/// <c>PngByteQRCode</c> is pure managed code, so it runs on Linux CI; the <c>QRCode</c> renderer needs System.Drawing and is never used).
/// </summary>
internal static class KdpQrCode
{
    /// <summary>Sharp edges at 0.8 in: a 37-module code (version 3 with its quiet zone) at 16 px a module is 592 px, about 740 dpi.</summary>
    public const int PixelsPerModule = 16;

    private static readonly byte[] Dark = [0x1B, 0x17, 0x2E, 0xFF];
    private static readonly byte[] Light = [0xFF, 0xFF, 0xFF, 0xFF];

    public static byte[] Png(string text)
    {
        using var data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(PixelsPerModule, Dark, Light, drawQuietZones: true);
    }
}
