using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>
/// The word lists of one language for the rules that read meaning: R2 sizes, R3 colours, R9 stretch (and, in later items, R5/R10 verbs and R6 meaning lists).
/// A language without a lexicon (<see cref="For"/> returns null) skips those rules quietly (Slice 46 decision 10); only English exists so far.
/// </summary>
public sealed class StoryLexicon
{
    public IReadOnlySet<string> Big { get; init; } = new HashSet<string>();

    /// <summary>Pairs of words that are big together ("great big"): both words take the big look.</summary>
    public IReadOnlyList<(string First, string Second)> BigPairs { get; init; } = [];

    public IReadOnlySet<string> Small { get; init; } = new HashSet<string>();

    public IReadOnlySet<string> Quiet { get; init; } = new HashSet<string>();

    public IReadOnlySet<string> Stretch { get; init; } = new HashSet<string>();

    /// <summary>R5: the verbs that attribute a quote to a speaker ("said Kathryn", "Seagull called"), lower-cased.</summary>
    public IReadOnlySet<string> SpeechVerbs { get; init; } = new HashSet<string>();

    /// <summary>R10: the attribution verbs that make a quote a song; a subset of <see cref="SpeechVerbs"/>.</summary>
    public IReadOnlySet<string> SongVerbs { get; init; } = new HashSet<string>();

    /// <summary>R6: words of a triplet that fall.</summary>
    public IReadOnlySet<string> Down { get; init; } = new HashSet<string>();

    /// <summary>R6: words of a triplet that rise.</summary>
    public IReadOnlySet<string> Up { get; init; } = new HashSet<string>();

    /// <summary>R6: words of a triplet that are a sound (calls, bangs, animal noises): loud, or quiet when a quiet word is in the same sentence.</summary>
    public IReadOnlySet<string> Sound { get; init; } = new HashSet<string>();

    /// <summary>R6: words of a triplet that shake.</summary>
    public IReadOnlySet<string> Shake { get; init; } = new HashSet<string>();

    /// <summary>R6: words of a triplet that bounce.</summary>
    public IReadOnlySet<string> Bounce { get; init; } = new HashSet<string>();

    /// <summary>Colour words and the base colour each is drawn in before it is darkened for its page.</summary>
    public IReadOnlyDictionary<string, Rgb> Colours { get; init; } = new Dictionary<string, Rgb>();

    public static readonly StoryLexicon English = new()
    {
        Big = Set("big", "bigger", "biggest", "huge", "enormous", "giant", "gigantic", "massive", "large"),
        BigPairs = [("great", "big")],
        Small = Set("little", "small", "tiny", "wee", "teeny", "littlest", "smallest"),
        Quiet = Set("softly", "gently", "quietly", "whisper", "whispers", "whispered", "whispering", "hush", "hushed"),
        Stretch = Set("long", "slow", "slowly", "sleepy", "sleepily", "drowsy"),
        SpeechVerbs = Set("said", "asked", "shouted", "called", "cried", "sang", "sings", "whispered", "replied", "says", "asks", "shouts", "calls", "cries", "replies", "answered", "yelled", "squeaked"),
        SongVerbs = Set("sang", "sings"),
        Down = Set("down", "fall", "falls", "fell", "falling", "drop", "drops", "dropped", "sink", "sinks", "sank", "tumble", "tumbled", "plop"),
        Up = Set("up", "high", "climb", "climbs", "climbed", "rise", "rises", "rose", "soar", "soared", "higher"),
        Sound = Set("calls", "called", "bang", "bangs", "banged", "boom", "crash", "squawk", "moo", "quack", "woof", "meow", "oink", "baa", "roar", "cheep", "tweet", "neigh", "honk", "hoot", "cluck", "buzz", "ring", "knock", "ding", "dong"),
        Shake = Set("rumble", "rumbles", "wobble", "wobbles", "shiver", "shivers", "shake", "shakes", "tremble", "trembles", "quiver", "rattle", "rattles"),
        Bounce = Set("splash", "hop", "hops", "bounce", "bounces", "jump", "jumps", "boing", "skip", "skips", "splish", "splosh"),
        Colours = new Dictionary<string, Rgb>
        {
            ["red"] = new(0xC6, 0x28, 0x28),
            ["blue"] = new(0x15, 0x65, 0xC0),
            ["green"] = new(0x2E, 0x7D, 0x32),
            ["yellow"] = new(0xB8, 0x86, 0x0B),
            ["gold"] = new(0xB8, 0x86, 0x0B),
            ["orange"] = new(0xE6, 0x51, 0x00),
            ["purple"] = new(0x6A, 0x1B, 0x9A),
            ["pink"] = new(0xC2, 0x18, 0x5B),
            ["brown"] = new(0x6D, 0x4C, 0x41),
        },
    };

    /// <summary>The lexicon for a language code (<c>en</c>, <c>en-GB</c>), or null when the language has none.</summary>
    public static StoryLexicon? For(string? language)
    {
        var primary = (language ?? string.Empty).Split('-', '_')[0].ToLowerInvariant();
        return primary == "en" ? English : null;
    }

    private static HashSet<string> Set(params string[] words) => new(words, StringComparer.Ordinal);
}
