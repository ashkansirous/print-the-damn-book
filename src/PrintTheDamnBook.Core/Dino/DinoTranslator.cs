using System.Text;

namespace PrintTheDamnBook.Core.Dino;

/// <summary>
/// Puts a page's words into another language using a Dino book's translation table, which is keyed by whole sentences
/// (the pieces Dino Reader reads in). Quotes and spaces around a sentence stay; a sentence with no entry stays as written.
/// The table is indexed once (keys grouped by their first character, longest first), so a page costs one pass over its text.
/// </summary>
public sealed class DinoTranslator
{
    private readonly IReadOnlyDictionary<string, string> _table;
    private readonly Dictionary<char, string[]> _keysByFirstChar;

    public DinoTranslator(IReadOnlyDictionary<string, string> table)
    {
        _table = table;
        _keysByFirstChar = table.Keys
            .Where(k => k.Length > 0)
            .GroupBy(k => k[0])
            .ToDictionary(g => g.Key, g => g.OrderByDescending(k => k.Length).ToArray());
    }

    public string Translate(string text)
    {
        var result = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var key = KeyAt(text, i);
            if (key is null)
            {
                result.Append(text[i++]);
                continue;
            }

            result.Append(Replacement(key));
            i += key.Length;
        }

        return result.ToString();
    }

    /// <summary>The translation with the key's own leading and trailing spaces and line breaks put back, so neighbouring text never joins up.</summary>
    private string Replacement(string key)
    {
        var core = key.AsSpan().Trim();
        var start = key.Length - key.AsSpan().TrimStart().Length;
        var end = key.Length - key.AsSpan().TrimEnd().Length;
        return core.IsEmpty ? key : key[..start] + _table[key].Trim() + key[^end..];
    }

    private string? KeyAt(string text, int index)
    {
        return _keysByFirstChar.TryGetValue(text[index], out var keys)
            ? keys.FirstOrDefault(k => string.CompareOrdinal(text, index, k, 0, k.Length) == 0)
            : null;
    }
}
