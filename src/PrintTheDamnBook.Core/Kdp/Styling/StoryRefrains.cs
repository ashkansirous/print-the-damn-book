namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>One clause of a paragraph: the token range it covers and its words compared case- and punctuation-free.</summary>
public sealed record StoryClause(int FirstToken, int LastToken, int WordCount, string Key);

/// <summary>R7 (language-free): a clause of three or more words found twice or more anywhere in the book is a refrain. Whole clauses only, so "said Kathryn" is never one.</summary>
public static class StoryRefrains
{
    /// <summary>The clauses of a paragraph: text between <c>. ! ? ; :</c> and quote marks, commas kept inside.</summary>
    public static IReadOnlyList<StoryClause> Clauses(IReadOnlyList<StoryToken> tokens)
    {
        var clauses = new List<StoryClause>();
        var first = -1;
        var last = -1;
        var keys = new List<string>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].IsWord)
            {
                first = first < 0 ? i : first;
                last = i;
                keys.Add(tokens[i].Key);
            }
            else if (StoryBoundaries.HasBoundary(tokens[i].Text))
            {
                Close(clauses, first, last, keys);
                first = -1;
                keys = [];
            }
        }

        Close(clauses, first, last, keys);
        return clauses;
    }

    /// <summary>The keys of the clauses (of <see cref="StyleSpec.RefrainMinWords"/> or more words) that occur <see cref="StyleSpec.RefrainMinCount"/> times or more over the whole book.</summary>
    public static IReadOnlySet<string> Find(IEnumerable<string> pageTexts, StoryVoices? voices = null)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var paragraph in pageTexts.SelectMany(t => t.Split('\n')))
        {
            var tokens = StoryTokens.Split(paragraph);
            foreach (var clause in Clauses(tokens).Where(c => c.WordCount >= StyleSpec.RefrainMinWords && !IsAttribution(voices, tokens, c)))
            {
                counts[clause.Key] = counts.GetValueOrDefault(clause.Key) + 1;
            }
        }

        return counts.Where(c => c.Value >= StyleSpec.RefrainMinCount).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>An attribution clause (a speech verb next to a cast name: "sang Pancake Polly") is never a refrain, however long (decision 4); with no cast known nothing is.</summary>
    private static bool IsAttribution(StoryVoices? voices, IReadOnlyList<StoryToken> tokens, StoryClause clause)
    {
        return voices is not null && voices.IsAttribution(tokens, clause);
    }

    private static void Close(List<StoryClause> clauses, int first, int last, List<string> keys)
    {
        if (first >= 0)
        {
            clauses.Add(new StoryClause(first, last, keys.Count, string.Join(' ', keys)));
        }
    }
}
