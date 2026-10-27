using System.Collections.Concurrent;
using System.Globalization;

namespace PrintTheDamnBook.Core.Dino;

/// <summary>One page of a Dino Reader book: its words in the book's own language and the name of its picture inside the package.</summary>
public sealed record DinoPage(string Text, string ImageFile);

/// <summary>A language a Dino book can be read in. Right-to-left ones are listed but cannot be printed yet.</summary>
public sealed record DinoLanguage(string Code, string Name, bool IsRightToLeft, bool IsOwn);

/// <summary>
/// What this app reads of a <c>.dino</c> book (Slice 15): title, language, cover picture, pages with words, and the translation tables
/// (language code to source sentence to translated sentence). Cast, voices and recordings are ignored.
/// </summary>
public sealed record DinoBook(
    string Id,
    string Title,
    string Language,
    string? CoverFile,
    IReadOnlyList<DinoPage> Pages,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Translations,
    int SkippedPages = 0)
{
    private readonly ConcurrentDictionary<string, DinoTranslator> _translators = new();

    /// <summary>The book's own language first, then each translation in the file.</summary>
    public IReadOnlyList<DinoLanguage> Languages()
    {
        var own = Primary(Language);
        var list = new List<DinoLanguage> { Describe(own, isOwn: true) };
        list.AddRange(Translations.Keys.Select(Primary).Where(code => code != own).Distinct().Select(code => Describe(code, isOwn: false)));
        return list;
    }

    /// <summary>
    /// The language the words start in: the book's own, or, when that one is right-to-left (not printable yet), the first translation that is not.
    /// Null when every language in the book is right-to-left.
    /// </summary>
    public DinoLanguage? StartLanguage()
    {
        return Languages().FirstOrDefault(l => !l.IsRightToLeft);
    }

    /// <summary>A language by its code, as the language select names it.</summary>
    public static DinoLanguage DescribeLanguage(string code)
    {
        return Describe(Primary(code), isOwn: false);
    }

    /// <summary>True when <paramref name="code"/> is the book's own language (or empty).</summary>
    public bool IsOwn(string? code) => string.IsNullOrWhiteSpace(code) || Primary(code) == Primary(Language);

    /// <summary><paramref name="text"/> (written in the book's own language) in <paramref name="code"/>; unchanged for the own language or an unknown one.</summary>
    public string In(string text, string? code)
    {
        if (IsOwn(code))
        {
            return text;
        }

        var primary = Primary(code);
        var table = Translations.FirstOrDefault(t => Primary(t.Key) == primary).Value;
        return table is { Count: > 0 } ? _translators.GetOrAdd(primary, _ => new DinoTranslator(table)).Translate(text) : text;
    }

    private static DinoLanguage Describe(string code, bool isOwn)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            var name = culture.NativeName;
            return new DinoLanguage(code, name.Length > 0 ? char.ToUpper(name[0], culture) + name[1..] : code, culture.TextInfo.IsRightToLeft, isOwn);
        }
        catch (CultureNotFoundException)
        {
            return new DinoLanguage(code, code, false, isOwn);
        }
    }

    /// <summary>"fa-IR" and "FA" are both "fa".</summary>
    public static string Primary(string? code)
    {
        var trimmed = (code ?? string.Empty).Trim().ToLowerInvariant();
        var cut = trimmed.IndexOfAny(['-', '_']);
        return cut < 0 ? trimmed : trimmed[..cut];
    }
}

/// <summary>Why a <c>.dino</c> file could not be used; the message is meant for the user.</summary>
public sealed class DinoBookException(string message, Exception? inner = null) : Exception(message, inner);
