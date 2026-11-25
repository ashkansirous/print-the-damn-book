using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>The speaker colour rules of <c>docs/design/kdp-typography.md</c>: dark enough on white and on every page tint, not red, clearly different from the other speakers and from the R4 colours.</summary>
public static class SpeakerColourRules
{
    private static readonly Rgb White = new(0xFF, 0xFF, 0xFF);

    /// <summary>Not red (desk): hue inside 345 to 45 degrees counts as red once the colour has some chroma.</summary>
    private const double RedHueFrom = 345;
    private const double RedHueTo = 45;
    private const double RedMinChroma = 0.08;

    /// <summary>Differs clearly (desk): the least OKLab distance between two speaker colours, and between a speaker colour and an R4 colour.</summary>
    public const double MinDistance = 0.10;

    /// <summary>Why <paramref name="hex"/> cannot be a speaker colour, or null when it can. <paramref name="tints"/> are the page tints of the book; <paramref name="others"/> the other speakers' colours.</summary>
    public static string? Check(string hex, IReadOnlyList<Rgb> tints, IEnumerable<string> others, bool againstTriplets = true)
    {
        if (!KdpHexColor.TryNormalize(hex, out var normal))
        {
            return $"\"{hex}\" is not a colour; use #RRGGBB.";
        }

        var colour = StyleColors.Parse(normal);
        return Contrast(normal, colour, tints) ?? Red(normal, colour) ?? Distinct(normal, colour, againstTriplets ? others.Concat(StyleSpec.TripletColors) : others);
    }

    private static string? Contrast(string hex, Rgb colour, IReadOnlyList<Rgb> tints)
    {
        var white = StyleColors.Contrast(colour, White);
        if (white < StyleSpec.MinContrast)
        {
            return $"{hex} is too light: contrast {white:0.0}:1 on white, 4.5:1 is needed.";
        }

        foreach (var tint in tints)
        {
            var ratio = StyleColors.Contrast(colour, tint);
            if (ratio < StyleSpec.MinContrast)
            {
                return $"{hex} is too light on the page tint {StyleColors.ToHex(tint)}: contrast {ratio:0.0}:1, 4.5:1 is needed.";
            }
        }

        return null;
    }

    private static string? Red(string hex, Rgb colour)
    {
        var (_, a, b) = Oklab.From(colour);
        var chroma = Math.Sqrt(a * a + b * b);
        var hue = (Math.Atan2(b, a) * 180 / Math.PI + 360) % 360;
        return chroma > RedMinChroma && (hue >= RedHueFrom || hue <= RedHueTo) ? $"{hex} is red; speaker colours are never red." : null;
    }

    private static string? Distinct(string hex, Rgb colour, IEnumerable<string> others)
    {
        foreach (var other in others)
        {
            if (Distance(colour, StyleColors.Parse(other)) < MinDistance)
            {
                return $"{hex} is too close to {other}, which is another speaker's or a triplet colour.";
            }
        }

        return null;
    }

    public static double Distance(Rgb x, Rgb y)
    {
        var (l1, a1, b1) = Oklab.From(x);
        var (l2, a2, b2) = Oklab.From(y);
        return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
    }
}

/// <summary>What <see cref="SpeakerPalette.Resolve"/> decided: the colour of each speaker, who got the fallback palette, and the saved colours that were refused (name to reason).</summary>
public sealed record SpeakerPaletteResult(IReadOnlyDictionary<string, string> Colors, IReadOnlyList<string> FallbackNames, IReadOnlyDictionary<string, string> Rejected);

/// <summary>Gives every speaker of a book a colour: the one saved in <c>kdp.json</c> when it passes the rules, else the next fallback palette colour that does (Slice 46 item E).</summary>
public static class SpeakerPalette
{
    /// <summary>The five colours of the owner's spec, then five more (desk).</summary>
    public static readonly IReadOnlyList<string> Fallback =
        ["#1F4FA0", "#0E6B78", "#6A3A96", "#2F6B34", "#8A4B12", "#5B3A8C", "#1D5C5C", "#7A4A2A", "#3A4F8A", "#4A6B1F"];

    private const double HueStepDegrees = 15;
    private static readonly double[] GeneratedLightnesses = [0.42, 0.34, 0.50];
    private const double GeneratedChroma = 0.11;

    public static SpeakerPaletteResult Resolve(IReadOnlyList<SpeakerInfo> speakers, IReadOnlyDictionary<string, string> saved, IReadOnlyList<Rgb> tints)
    {
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        var rejected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var speaker in speakers.Where(s => saved.ContainsKey(s.Name)))
        {
            var hex = saved[speaker.Name];
            var why = SpeakerColourRules.Check(hex, tints, colors.Values);
            if (why is null)
            {
                colors[speaker.Name] = hex.ToUpperInvariant();
            }
            else
            {
                rejected[speaker.Name] = why;
            }
        }

        var fallback = new List<string>();
        foreach (var speaker in speakers.Where(s => !colors.ContainsKey(s.Name)))
        {
            colors[speaker.Name] = NextFallback(tints, colors.Values.ToList());
            fallback.Add(speaker.Name);
        }

        return new SpeakerPaletteResult(colors, fallback, rejected);
    }

    private static string NextFallback(IReadOnlyList<Rgb> tints, List<string> taken)
    {
        var fromPalette = Fallback.FirstOrDefault(c => SpeakerColourRules.Check(c, tints, taken, againstTriplets: false) is null);
        return fromPalette ?? Generated(tints, taken);
    }

    /// <summary>
    /// A palette that ran out (a book with more speakers than colours): of the colours round the hue circle that read and are not red, the one furthest from those already
    /// taken, so no two speakers share a colour even when none is clearly different any more.
    /// </summary>
    private static string Generated(IReadOnlyList<Rgb> tints, List<string> taken)
    {
        var candidates = GeneratedColours().Where(c => SpeakerColourRules.Check(c, tints, [], againstTriplets: false) is null).ToList();
        var takenRgb = taken.Select(StyleColors.Parse).ToList();
        var best = candidates.Where(c => !taken.Contains(c)).OrderByDescending(c => takenRgb.Count == 0 ? 1 : takenRgb.Min(t => SpeakerColourRules.Distance(StyleColors.Parse(c), t))).FirstOrDefault();
        return best ?? StyleColors.Ink;
    }

    private static IEnumerable<string> GeneratedColours()
    {
        foreach (var lightness in GeneratedLightnesses)
        {
            for (var hue = 0.0; hue < 360; hue += HueStepDegrees)
            {
                var radians = hue * Math.PI / 180;
                yield return StyleColors.ToHex(Oklab.ToRgb(lightness, GeneratedChroma * Math.Cos(radians), GeneratedChroma * Math.Sin(radians)));
            }
        }
    }
}
