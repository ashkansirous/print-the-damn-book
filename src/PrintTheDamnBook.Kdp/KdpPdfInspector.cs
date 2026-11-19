using System.Text;
using System.Text.RegularExpressions;

namespace PrintTheDamnBook.Kdp;

/// <summary>How many fonts a PDF names and how many of them carry their glyphs inside the file (KDP rejects unembedded fonts).</summary>
public sealed record KdpFontCheck(int Fonts, int Embedded, IReadOnlyList<string> Names)
{
    public bool AllEmbedded => Fonts > 0 && Embedded == Fonts;
}

/// <summary>Reads the font descriptors of a PDF made with <c>CompressDocument = false</c>, where they are plain text.</summary>
internal static partial class KdpPdfInspector
{
    public static KdpFontCheck CheckFonts(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var descriptors = DescriptorPattern().Matches(text);
        var embedded = descriptors.Count(m => FontFilePattern().IsMatch(m.Value));
        var names = descriptors.Select(m => FontNamePattern().Match(m.Value)).Where(m => m.Success).Select(m => m.Groups[1].Value).Distinct().ToList();
        return new KdpFontCheck(descriptors.Count, embedded, names);
    }

    [GeneratedRegex(@"<<[^>]*?/Type\s*/FontDescriptor.*?>>", RegexOptions.Singleline)]
    private static partial Regex DescriptorPattern();

    [GeneratedRegex(@"/FontFile[23]?\s")]
    private static partial Regex FontFilePattern();

    [GeneratedRegex(@"/FontName\s*/([^\s/>]+)")]
    private static partial Regex FontNamePattern();
}
