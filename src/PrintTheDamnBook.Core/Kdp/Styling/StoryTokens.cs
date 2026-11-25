using System.Text;

namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>A piece of a paragraph: a word (letters and digits, with an apostrophe, hyphen or zero-width non-joiner between letters kept inside it) or the gap of spaces and punctuation between two words.</summary>
public sealed record StoryToken(string Text, bool IsWord)
{
    /// <summary>The word lower-cased with curly apostrophes made straight; what the word lists and comparisons use.</summary>
    public string Key => Text.ToLowerInvariant().Replace('’', '\'');

    /// <summary>Only the letters and digits of the word, lower-cased: what the triplet test compares.</summary>
    public string Letters => new(Key.Where(char.IsLetterOrDigit).ToArray());
}

/// <summary>Splits one paragraph into <see cref="StoryToken"/>s, losslessly: joining the tokens gives the paragraph back.</summary>
public static class StoryTokens
{
    public static IReadOnlyList<StoryToken> Split(string paragraph)
    {
        var tokens = new List<StoryToken>();
        var at = 0;
        while (at < paragraph.Length)
        {
            var isWord = char.IsLetterOrDigit(paragraph[at]);
            var end = isWord ? WordEnd(paragraph, at) : GapEnd(paragraph, at);
            tokens.Add(new StoryToken(paragraph[at..end], isWord));
            at = end;
        }

        return tokens;
    }

    private static int WordEnd(string text, int start)
    {
        var end = start;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || IsInnerMark(text, end)))
        {
            end++;
        }

        return end;
    }

    private static bool IsInnerMark(string text, int at)
    {
        var mark = text[at];
        return (mark is '\'' or '’' or '-' or '‌') && at > 0 && at + 1 < text.Length && char.IsLetter(text[at - 1]) && char.IsLetter(text[at + 1]);
    }

    private static int GapEnd(string text, int start)
    {
        var end = start;
        while (end < text.Length && !char.IsLetterOrDigit(text[end]))
        {
            end++;
        }

        return end;
    }

    /// <summary>Joins tokens back to text (for tests and diagnostics).</summary>
    public static string Join(IEnumerable<StoryToken> tokens)
    {
        var sb = new StringBuilder();
        foreach (var t in tokens)
        {
            sb.Append(t.Text);
        }

        return sb.ToString();
    }
}
