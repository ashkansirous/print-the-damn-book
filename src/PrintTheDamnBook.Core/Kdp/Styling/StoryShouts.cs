namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>R8 (language-free): one to three words at the start of a quote that end in <c>!</c> ("Squawk!", "Stop, stop!" is not one: its comma ends the first word).</summary>
public static class StoryShouts
{
    private const string Enders = ".!?;:,\"“”…";

    /// <summary>The token indices of the words of each shout in a paragraph.</summary>
    public static IReadOnlyList<IReadOnlyList<int>> Find(IReadOnlyList<StoryToken> tokens)
    {
        var shouts = new List<IReadOnlyList<int>>();
        var inQuote = false;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].IsWord)
            {
                continue;
            }

            var opened = !inQuote;
            inQuote = Walk(tokens[i].Text, inQuote);
            if (opened && inQuote && Collect(tokens, i + 1) is { } shout)
            {
                shouts.Add(shout);
            }
        }

        return shouts;
    }

    /// <summary>The quote state after the marks in a gap: a straight quote flips it, a curly opening quote sets it, a curly closing one clears it.</summary>
    private static bool Walk(string gap, bool inQuote)
    {
        foreach (var c in gap)
        {
            inQuote = c switch
            {
                '"' => !inQuote,
                '“' => true,
                '”' => false,
                _ => inQuote,
            };
        }

        return inQuote;
    }

    /// <summary>The words from <paramref name="from"/> up to the first ending mark when that mark is <c>!</c> and there are 1 to <see cref="StyleSpec.ShoutMaxWords"/> of them; else null.</summary>
    private static IReadOnlyList<int>? Collect(IReadOnlyList<StoryToken> tokens, int from)
    {
        var words = new List<int>();
        for (var j = from; j < tokens.Count; j++)
        {
            if (tokens[j].IsWord)
            {
                words.Add(j);
                continue;
            }

            var end = tokens[j].Text.FirstOrDefault(c => Enders.Contains(c, StringComparison.Ordinal));
            if (end != default)
            {
                return end == '!' && words.Count is >= 1 and <= StyleSpec.ShoutMaxWords ? words : null;
            }
        }

        return null;
    }
}
