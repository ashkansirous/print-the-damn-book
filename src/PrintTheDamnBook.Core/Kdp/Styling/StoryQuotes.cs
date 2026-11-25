namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>One quotation of a paragraph: the token indices of its words, who says it (a cast name, or null) and whether the attribution verb makes it a song.</summary>
public sealed record StoryQuote(IReadOnlyList<int> Words, string? Speaker, bool HasVerb, bool IsSong);

/// <summary>
/// R5 and R10: the quotes of a paragraph and their speakers. A quote is attributed by a speech verb next to a cast name, in the clause right after
/// its closing mark ("…," said Seagull softly.) or, when that holds none, in the clause right before its opening mark (Kathryn said, "…"). A pronoun
/// ("she said") or no attribution leaves <see cref="StoryQuote.Speaker"/> null.
/// </summary>
public static class StoryQuotes
{
    public static IReadOnlyList<StoryQuote> Find(IReadOnlyList<StoryToken> tokens, StoryVoices voices)
    {
        var quotes = new List<StoryQuote>();
        foreach (var (open, close) in Spans(tokens))
        {
            var words = Enumerable.Range(open + 1, Math.Max(0, close - open - 1)).Where(i => tokens[i].IsWord).ToList();
            if (words.Count > 0)
            {
                quotes.Add(Attribute(tokens, voices, words, open, close));
            }
        }

        return quotes;
    }

    /// <summary>The gap indices where each quote opens and closes (the end of the paragraph closes one left open).</summary>
    private static List<(int Open, int Close)> Spans(IReadOnlyList<StoryToken> tokens)
    {
        var spans = new List<(int, int)>();
        var open = -1;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].IsWord)
            {
                continue;
            }

            foreach (var c in tokens[i].Text)
            {
                var opens = c == '“' || (c == '"' && open < 0);
                var closes = c == '”' || (c == '"' && open >= 0);
                if (opens && open < 0)
                {
                    open = i;
                }
                else if (closes && open >= 0)
                {
                    spans.Add((open, i));
                    open = -1;
                }
            }
        }

        if (open >= 0)
        {
            spans.Add((open, tokens.Count));
        }

        return spans;
    }

    private static StoryQuote Attribute(IReadOnlyList<StoryToken> tokens, StoryVoices voices, List<int> words, int open, int close)
    {
        var found = Look(tokens, voices, EndsClauseAfter(tokens, close) ? [] : ClauseWords(tokens, close + 1, forward: true));
        if (found.Verb < 0)
        {
            found = Look(tokens, voices, EndsClauseBefore(tokens, open) ? [] : ClauseWords(tokens, open - 1, forward: false));
        }

        return new StoryQuote(words, found.Name?.Name, found.Verb >= 0, found.Verb >= 0 && found.Song);
    }

    private static readonly char[] QuoteMarks = ['"', '“', '”'];

    /// <summary>True when a mark that ends a clause sits in the closing gap after the closing quote mark (so the next words are a new sentence).</summary>
    private static bool EndsClauseAfter(IReadOnlyList<StoryToken> tokens, int close)
    {
        if (close >= tokens.Count)
        {
            return true;
        }

        var gap = tokens[close].Text;
        return StoryBoundaries.HasBoundary(gap[(gap.IndexOfAny(QuoteMarks) + 1)..]);
    }

    /// <summary>True when a mark that ends a clause sits in the opening gap before the opening quote mark.</summary>
    private static bool EndsClauseBefore(IReadOnlyList<StoryToken> tokens, int open)
    {
        var gap = tokens[open].Text;
        return StoryBoundaries.HasBoundary(gap[..gap.LastIndexOfAny(QuoteMarks)]);
    }

    private static (int Verb, CastName? Name, bool Song) Look(IReadOnlyList<StoryToken> tokens, StoryVoices voices, List<int> window)
    {
        for (var w = 0; w < window.Count; w++)
        {
            if (voices.IsSpeechVerb(tokens[window[w]]))
            {
                return (w, voices.Beside(tokens, window, w), voices.IsSongVerb(tokens[window[w]]));
            }
        }

        return (-1, null, false);
    }

    /// <summary>The word token indices from <paramref name="from"/> up to (or back to) the next mark that ends a clause, in reading order.</summary>
    private static List<int> ClauseWords(IReadOnlyList<StoryToken> tokens, int from, bool forward)
    {
        var words = new List<int>();
        for (var i = from; i >= 0 && i < tokens.Count; i += forward ? 1 : -1)
        {
            if (tokens[i].IsWord)
            {
                words.Add(i);
            }
            else if (StoryBoundaries.HasBoundary(tokens[i].Text))
            {
                break;
            }
        }

        if (!forward)
        {
            words.Reverse();
        }

        return words;
    }
}
