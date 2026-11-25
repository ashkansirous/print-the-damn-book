namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>R4 (language-free): three words in a row, punctuation between them allowed, that are equal or that share their first two and last two letters (splish, splash, splosh).</summary>
public static class StoryTriplets
{
    /// <summary>The token indices of each triplet's three words, left to right. A word belongs to at most one triplet; the scan restarts after each.</summary>
    public static IReadOnlyList<(int First, int Second, int Third)> Find(IReadOnlyList<StoryToken> tokens)
    {
        var words = Enumerable.Range(0, tokens.Count).Where(i => tokens[i].IsWord).ToList();
        var found = new List<(int, int, int)>();
        var at = 0;
        while (at + 2 < words.Count)
        {
            var trio = (tokens[words[at]].Letters, tokens[words[at + 1]].Letters, tokens[words[at + 2]].Letters);
            if (Alike(trio.Item1, trio.Item2, trio.Item3))
            {
                found.Add((words[at], words[at + 1], words[at + 2]));
                at += 3;
            }
            else
            {
                at++;
            }
        }

        return found;
    }

    /// <summary>Equal words, or all at least <see cref="StyleSpec.TripletMinLetters"/> long and sharing the first and the last <see cref="StyleSpec.TripletSharedEdge"/> letters.</summary>
    public static bool Alike(string a, string b, string c)
    {
        if (a.Length == 0 || b.Length == 0 || c.Length == 0)
        {
            return false;
        }

        if (a == b && b == c)
        {
            return true;
        }

        var words = new[] { a, b, c };
        var edge = StyleSpec.TripletSharedEdge;
        return words.All(w => w.Length >= StyleSpec.TripletMinLetters)
            && words.All(w => w[..edge] == a[..edge] && w[^edge..] == a[^edge..]);
    }
}
