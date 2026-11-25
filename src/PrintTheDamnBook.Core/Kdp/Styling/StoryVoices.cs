namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>A cast name split into the word keys the story's tokens are compared with ("Pancake Polly" is two).</summary>
public sealed record CastName(string Name, IReadOnlyList<string> Keys);

/// <summary>
/// The speakers of a book (Slice 46 decision 1): its cast names and the language's attribution verbs. R5 voices, R10 songs and the R7 exception for
/// attribution clauses all ask it. It exists only for a language with a lexicon and a book with a cast; <see cref="For"/> returns null otherwise and
/// the rules are skipped quietly.
/// </summary>
public sealed class StoryVoices
{
    private readonly IReadOnlyList<CastName> _names;
    private readonly StoryLexicon _lexicon;

    private StoryVoices(IReadOnlyList<CastName> cast, StoryLexicon lexicon)
    {
        Cast = cast;
        _names = cast.OrderByDescending(n => n.Keys.Count).ToList();
        _lexicon = lexicon;
    }

    /// <summary>The cast names in the book's own order.</summary>
    public IReadOnlyList<CastName> Cast { get; }

    public static StoryVoices? For(IEnumerable<string>? cast, string language)
    {
        var lexicon = StoryLexicon.For(language);
        var names = (cast ?? []).Where(n => !string.IsNullOrWhiteSpace(n)).Select(Split).Where(n => n.Keys.Count > 0).ToList();
        return lexicon is null || lexicon.SpeechVerbs.Count == 0 || names.Count == 0 ? null : new StoryVoices(names, lexicon);
    }

    public bool IsSpeechVerb(StoryToken word) => _lexicon.SpeechVerbs.Contains(word.Key);

    public bool IsSongVerb(StoryToken word) => _lexicon.SongVerbs.Contains(word.Key);

    /// <summary>The longest cast name whose words start at <paramref name="at"/> in <paramref name="words"/> (token indices of words), with only spaces and marks that do not end a clause between them; null when none.</summary>
    public CastName? NameAt(IReadOnlyList<StoryToken> tokens, IReadOnlyList<int> words, int at)
    {
        foreach (var name in _names)
        {
            if (Matches(tokens, words, at, name))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>True when the clause holds a speech verb next to a cast name: "sang Pancake Polly", "said Seagull softly". Such a clause is never a refrain (decision 4).</summary>
    public bool IsAttribution(IReadOnlyList<StoryToken> tokens, StoryClause clause)
    {
        var words = Enumerable.Range(clause.FirstToken, clause.LastToken - clause.FirstToken + 1).Where(i => tokens[i].IsWord).ToList();
        for (var w = 0; w < words.Count; w++)
        {
            if (IsSpeechVerb(tokens[words[w]]) && Beside(tokens, words, w) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The cast name right after the verb at word position <paramref name="verb"/>, or the one that ends right before it; null when neither.</summary>
    public CastName? Beside(IReadOnlyList<StoryToken> tokens, IReadOnlyList<int> words, int verb)
    {
        if (verb + 1 < words.Count && NameAt(tokens, words, verb + 1) is { } after)
        {
            return after;
        }

        for (var start = verb - 1; start >= 0 && start >= verb - MaxNameWords; start--)
        {
            if (NameAt(tokens, words, start) is { } before && start + before.Keys.Count == verb)
            {
                return before;
            }
        }

        return null;
    }

    private int MaxNameWords => _names.Max(n => n.Keys.Count);

    private static bool Matches(IReadOnlyList<StoryToken> tokens, IReadOnlyList<int> words, int at, CastName name)
    {
        if (at + name.Keys.Count > words.Count)
        {
            return false;
        }

        for (var k = 0; k < name.Keys.Count; k++)
        {
            if (tokens[words[at + k]].Key != name.Keys[k] || (k > 0 && !JoinsWords(tokens, words[at + k - 1], words[at + k])))
            {
                return false;
            }
        }

        return true;
    }

    private static bool JoinsWords(IReadOnlyList<StoryToken> tokens, int from, int to)
    {
        return to == from + 2 && !StoryBoundaries.HasBoundary(tokens[from + 1].Text) && !tokens[from + 1].Text.Contains(',', StringComparison.Ordinal);
    }

    private static CastName Split(string name)
    {
        var keys = StoryTokens.Split(name.Trim()).Where(t => t.IsWord).Select(t => t.Key).ToList();
        return new CastName(name.Trim(), keys);
    }
}
