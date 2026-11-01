using System.Text;

namespace PrintTheDamnBook.Core.Text;

/// <summary>
/// Which way text and books run (Slice 17). A text's direction is that of its first strong letter, as Dino Reader decides it; a
/// book's is the reader's pick, else the language of a Dino book, else a guess from the first story text.
/// </summary>
public static class TextDirection
{
    /// <summary>The right-to-left languages this app can set in type (Arabic script, in Vazirmatn). Others, such as Hebrew, stay disabled.</summary>
    private static readonly string[] SupportedRightToLeftLanguages = ["fa", "ar"];

    /// <summary>True when the first strong (letter) character is right-to-left. Empty, digits-only and punctuation-only text is left to right.</summary>
    public static bool StartsRightToLeft(string? text)
    {
        foreach (var rune in (text ?? string.Empty).EnumerateRunes())
        {
            if (Rune.IsLetter(rune))
            {
                return IsRightToLeftLetter(rune.Value);
            }
        }

        return false;
    }

    public static ReadingDirection Of(string? text) => StartsRightToLeft(text) ? ReadingDirection.RightToLeft : ReadingDirection.LeftToRight;

    /// <summary>True when the text has at least one letter of the Arabic script (Arabic, Persian, Urdu ...).</summary>
    public static bool HasArabicScriptLetter(string? text)
    {
        return (text ?? string.Empty).EnumerateRunes().Any(r => Rune.IsLetter(r) && IsArabicScript(r.Value));
    }

    /// <summary>True when books in this language (primary code, "fa" or "fa-IR") can be printed right to left.</summary>
    public static bool IsSupportedRightToLeftLanguage(string? code)
    {
        return Dino.DinoBook.Primary(code) is { Length: > 0 } primary && SupportedRightToLeftLanguages.Contains(primary);
    }

    /// <summary>The direction a book in this language opens from: right to left for Farsi and Arabic, otherwise left to right.</summary>
    public static ReadingDirection ForLanguage(string? code)
    {
        return IsSupportedRightToLeftLanguage(code) ? ReadingDirection.RightToLeft : ReadingDirection.LeftToRight;
    }

    /// <summary>True when the text has a letter only Persian uses (U+067E, U+0686, U+0698, U+06AF, U+06A9, U+06CC), which Arabic spells differently.</summary>
    public static bool HasPersianOnlyLetter(string? text)
    {
        return (text ?? string.Empty).EnumerateRunes().Any(r => r.Value is 0x067E or 0x0686 or 0x0698 or 0x06AF or 0x06A9 or 0x06CC);
    }

    /// <summary>
    /// The direction of the first text with words (left to right when there is none). Only Arabic-script text guesses right to left:
    /// the app has a font for no other right-to-left script, so a Hebrew text is not guessed.
    /// </summary>
    public static ReadingDirection Guess(IEnumerable<string?> textsInBookOrder)
    {
        var first = textsInBookOrder.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        return StartsRightToLeft(first) && HasArabicScriptLetter(first) ? ReadingDirection.RightToLeft : ReadingDirection.LeftToRight;
    }

    /// <summary>
    /// The direction a book opens from. The reader's <paramref name="pick"/> always wins and is never replaced; without one a Dino book
    /// follows <paramref name="dinoLanguage"/> (null for a folder book), and a folder book is guessed from its story texts.
    /// </summary>
    public static ReadingDirection Resolve(ReadingDirection? pick, string? dinoLanguage, IEnumerable<string?> textsInBookOrder)
    {
        if (pick is { } picked)
        {
            return picked;
        }

        return dinoLanguage is null ? Guess(textsInBookOrder) : ForLanguage(dinoLanguage);
    }

    private static bool IsRightToLeftLetter(int code)
    {
        return code is (>= 0x0590 and <= 0x08FF) or (>= 0xFB1D and <= 0xFDFF) or (>= 0xFE70 and <= 0xFEFF);
    }

    private static bool IsArabicScript(int code)
    {
        return code is (>= 0x0600 and <= 0x06FF) or (>= 0x0750 and <= 0x077F) or (>= 0x08A0 and <= 0x08FF) or (>= 0xFB50 and <= 0xFDFF) or (>= 0xFE70 and <= 0xFEFF);
    }
}
