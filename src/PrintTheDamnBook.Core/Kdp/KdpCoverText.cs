using System.Globalization;
using System.Text.Json.Serialization;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>A box on a cover's trim panel in fractions of the panel (0 to 1, from its top-left corner), so it survives a crop change (Slice 37).</summary>
public sealed record KdpTextBox(
    [property: JsonPropertyName("left"), JsonRequired] double Left,
    [property: JsonPropertyName("top"), JsonRequired] double Top,
    [property: JsonPropertyName("width"), JsonRequired] double Width,
    [property: JsonPropertyName("height"), JsonRequired] double Height)
{
    private const double Slack = 1e-9;

    /// <summary>All four numbers are real, the box has a size and lies inside the panel.</summary>
    [JsonIgnore]
    public bool IsValid =>
        double.IsFinite(Left) && double.IsFinite(Top) && double.IsFinite(Width) && double.IsFinite(Height)
        && Left >= 0 && Top >= 0 && Width > 0 && Height > 0 && Left + Width <= 1 + Slack && Top + Height <= 1 + Slack;
}

/// <summary>A text colour and the colour of the soft shadow under it, both <c>#RRGGBB</c> (see <see cref="KdpHexColor"/>).</summary>
public sealed record KdpTextColors(
    [property: JsonPropertyName("color"), JsonRequired] string Color,
    [property: JsonPropertyName("shadow"), JsonRequired] string Shadow);

/// <summary>What the crop agent chose for the words of one cover: the empty space they go in, the text colour and the shadow colour.</summary>
public sealed record KdpCoverText(
    [property: JsonPropertyName("box"), JsonRequired] KdpTextBox Box,
    [property: JsonPropertyName("color"), JsonRequired] string Color,
    [property: JsonPropertyName("shadow"), JsonRequired] string Shadow)
{
    [JsonIgnore]
    public KdpTextColors Colors => new(Color, Shadow);
}

/// <summary>Reads and checks the <c>#RRGGBB</c> colours of the cover text choices.</summary>
public static class KdpHexColor
{
    private const int Digits = 6;

    /// <summary>Accepts <c>RRGGBB</c> or <c>#RRGGBB</c> in any case and returns <c>#RRGGBB</c> in upper case; false for anything else.</summary>
    public static bool TryNormalize(string? text, out string normalized)
    {
        normalized = string.Empty;
        var digits = text?.TrimStart('#') ?? string.Empty;
        if (digits.Length != Digits || (text!.Length - digits.Length) > 1 || !digits.All(Uri.IsHexDigit))
        {
            return false;
        }

        normalized = "#" + digits.ToUpperInvariant();
        return true;
    }

    /// <summary>The colour as ARGB bytes-free text for a PDF or image library: the six digits without the <c>#</c>.</summary>
    public static string Digits6(string normalized) => normalized.TrimStart('#');

    /// <summary>Red, green and blue (0 to 255) of a normalized colour.</summary>
    public static (byte R, byte G, byte B) Rgb(string normalized)
    {
        var d = Digits6(normalized);
        return (Part(d, 0), Part(d, 2), Part(d, 4));
    }

    private static byte Part(string digits, int at) => byte.Parse(digits.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

/// <summary>
/// The box maths of the cover text (Slice 37): the agent's fractions of the trim panel become inches on the cover, kept inside the safe margin and clear of the
/// barcode. The Kdp project only draws into the box that comes out.
/// </summary>
public static class KdpTextBoxMaths
{
    /// <summary>Words stay this far inside the trim edge (KDP's safe zone, as on the interior).</summary>
    public const double SafeMarginInches = KdpTrim.SafeMarginInches;

    /// <summary>Words stay this far from the barcode box.</summary>
    public const double BarcodeClearInches = 0.25;

    /// <summary>A box smaller than this cannot hold words worth reading; the caller falls back to its own placement.</summary>
    public const double MinWidthInches = 1.0;

    public const double MinHeightInches = 0.3;

    /// <summary>The back cover's QR code and Dino line sit this far in from the trim edge.</summary>
    public const double PanelInsetInches = 0.5;

    /// <summary>The QR code on the back cover is this wide and tall.</summary>
    public const double QrInches = 0.8;

    /// <summary>
    /// The strip along the bottom of the back panel that holds the QR code and the Dino line (full width, with <see cref="BarcodeClearInches"/> above it),
    /// which words must stay clear of.
    /// </summary>
    public static RectD QrStrip(RectD backTrim)
    {
        var top = backTrim.Y + backTrim.Height - PanelInsetInches - QrInches - BarcodeClearInches;
        return new RectD(backTrim.X, top, backTrim.Width, backTrim.Y + backTrim.Height - top);
    }

    /// <summary>
    /// The box in inches on the cover: the fractions of <paramref name="trim"/>, cut to the safe margin and, when it meets the barcode (with its clearance),
    /// cut to whichever of the part above it or the part beside it is larger. Null when what is left is under <see cref="MinWidthInches"/> by
    /// <see cref="MinHeightInches"/>, or has lost more than half its height to the clearance (or the fractions are not a valid box); the caller warns and falls back.
    /// </summary>
    public static RectD? Resolve(KdpTextBox fractions, RectD trim, RectD? barcode, RectD? qrStrip = null)
    {
        if (!fractions.IsValid)
        {
            return null;
        }

        var raw = new RectD(trim.X + fractions.Left * trim.Width, trim.Y + fractions.Top * trim.Height, fractions.Width * trim.Width, fractions.Height * trim.Height);
        var safe = new RectD(trim.X + SafeMarginInches, trim.Y + SafeMarginInches, trim.Width - 2 * SafeMarginInches, trim.Height - 2 * SafeMarginInches);
        var box = Intersect(raw, safe);
        var keptHeight = box?.Height ?? 0;
        if (box is { } inside && barcode is { } code)
        {
            box = ClearOf(inside, Inflate(code, BarcodeClearInches));
        }

        if (box is { } afterCode && qrStrip is { } strip)
        {
            box = ClearOf(afterCode, strip);
        }

        return box is { Width: >= MinWidthInches, Height: >= MinHeightInches } usable && usable.Height * 2 >= keptHeight ? box : null;
    }

    /// <summary>The part of <paramref name="box"/> outside <paramref name="avoid"/>: the larger of the part above it and the part on the box's own side of it.</summary>
    internal static RectD? ClearOf(RectD box, RectD avoid)
    {
        if (Intersect(box, avoid) is null)
        {
            return box;
        }

        var above = new RectD(box.X, box.Y, box.Width, Math.Max(0, avoid.Y - box.Y));
        var boxCentre = box.X + box.Width / 2;
        var avoidCentre = avoid.X + avoid.Width / 2;
        var beside = boxCentre < avoidCentre
            ? new RectD(box.X, box.Y, Math.Max(0, avoid.X - box.X), box.Height)
            : new RectD(avoid.X + avoid.Width, box.Y, Math.Max(0, box.X + box.Width - (avoid.X + avoid.Width)), box.Height);
        var best = above.Width * above.Height >= beside.Width * beside.Height ? above : beside;
        return best.Width > 0 && best.Height > 0 ? best : null;
    }

    private static RectD Inflate(RectD r, double by) => new(r.X - by, r.Y - by, r.Width + 2 * by, r.Height + 2 * by);

    private static RectD? Intersect(RectD a, RectD b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return right > left && bottom > top ? new RectD(left, top, right - left, bottom - top) : null;
    }
}
