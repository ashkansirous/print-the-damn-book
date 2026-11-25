using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Kdp.Styling;

/// <summary>
/// What the book's per-book style choices came to (Slice 46 item E): who speaks (null voices: no cast or no word list for the language), each speaker's colour, the main
/// character and the saved speaker colours that were refused (name to reason; the build used the fallback palette for them).
/// </summary>
public sealed record KdpStyleOutcome(StoryVoices? Voices, IReadOnlyDictionary<string, string> SpeakerColours, string? MainCharacter, IReadOnlyDictionary<string, string> RejectedSpeakers, IReadOnlyList<string>? SpeakerOrder = null)
{
    /// <summary>The speaking characters, the one with most lines first (the order of the Dino page's names).</summary>
    public IReadOnlyList<string> Order => SpeakerOrder ?? [.. SpeakerColours.Keys];

    public static KdpStyleOutcome None { get; } = new(null, new Dictionary<string, string>(), null, new Dictionary<string, string>());
}

/// <summary>Works out the speakers, their colours and the main character of a book from its cast, its text and the choices saved in <c>kdp.json</c>, with the warnings for every fallback.</summary>
internal static class KdpStyleSetup
{
    public static KdpStyleOutcome Prepare(KdpBookInput book, IReadOnlyList<KdpPicture> pictures, KdpBookSettings settings, List<string> warnings)
    {
        var voices = StoryVoices.For(book.Cast, book.Language);
        var analysis = SpeakerAnalysis.Analyse(book.Texts, voices);
        if (voices is null || analysis.Speakers.Count == 0)
        {
            return new KdpStyleOutcome(voices, new Dictionary<string, string>(), null, new Dictionary<string, string>());
        }

        var tints = pictures.Select(p => p.Tint).OfType<Rgb>().Distinct().ToList();
        var palette = SpeakerPalette.Resolve(analysis.Speakers, settings.Styles.SpeakerColours, tints);
        var main = MainCharacter(analysis, voices, settings, warnings);
        warnings.AddRange(Warnings(analysis, palette));
        return new KdpStyleOutcome(voices, palette.Colors, main, palette.Rejected, [.. analysis.Speakers.Select(s => s.Name)]);
    }

    private static string MainCharacter(SpeakerAnalysis analysis, StoryVoices voices, KdpBookSettings settings, List<string> warnings)
    {
        var saved = settings.Styles.MainCharacter;
        if (saved is not null && voices.Cast.Any(c => c.Name == saved.Name))
        {
            if (analysis.Speakers.All(s => s.Name != saved.Name))
            {
                warnings.Add($"styles: the saved main character \"{saved.Name}\" has no quoted lines, so no speaker colour; The End stays in ink. Choose a speaker with --main-character.");
            }

            return saved.Name;
        }

        var fallback = analysis.MostLines!.Name;
        warnings.Add(saved is null
            ? $"styles: no saved main character (fallback): {fallback} has the most lines ({analysis.MostLines.Lines}); the kdp-style skill chooses one with --main-character."
            : $"styles: the saved main character \"{saved.Name}\" is not in the cast (fallback): {fallback} has the most lines.");
        return fallback;
    }

    private static IEnumerable<string> Warnings(SpeakerAnalysis analysis, SpeakerPaletteResult palette)
    {
        foreach (var (name, why) in palette.Rejected)
        {
            yield return $"styles: the saved colour of {name} is refused ({why}); the fallback palette is used for {name}.";
        }

        var notSaved = palette.FallbackNames.Where(n => !palette.Rejected.ContainsKey(n)).ToList();
        if (notSaved.Count > 0)
        {
            yield return $"styles: no saved colour for {string.Join(", ", notSaved)} (fallback palette used); the kdp-style skill chooses them with --speaker \"<name>=#RRGGBB\".";
        }

        if (analysis.PagesWithUnknownSpeaker.Count > 0)
        {
            yield return $"styles: no speaker found from the cast for a quote on story page(s) {string.Join(", ", analysis.PagesWithUnknownSpeaker)} (a pronoun or no attribution); it stays in ink.";
        }
    }
}
