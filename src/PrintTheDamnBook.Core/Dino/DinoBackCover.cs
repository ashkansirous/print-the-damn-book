using System.Globalization;
using System.Text.Json;

namespace PrintTheDamnBook.Core.Dino;

/// <summary>
/// The credits of a Dino book's back cover as the file keeps them (Dino Reader Slice 237): who wrote the story (several names joined with <c>;</c>,
/// or "traditional"), whether it is a retelling, who drew it and the month it was made. <see cref="NamesByLanguage"/> (Dino Reader Slice 405) holds
/// a name's spelling per language when the book has one.
/// </summary>
public sealed record DinoCredits(
    string StoryBy,
    bool Retold,
    string Illustration,
    int Year,
    int Month,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? NamesByLanguage = null)
{
    /// <summary>The name of the person who retells the classics and draws every book (Dino Reader's <c>BackCover.Maker</c>).</summary>
    public const string Maker = "Ashkan Sirous";

    /// <summary>The <see cref="StoryBy"/> of a tale nobody owns.</summary>
    public const string Traditional = "traditional";

    /// <summary>Several names in <see cref="StoryBy"/> are written with this between them.</summary>
    public const char NameSeparator = ';';

    public bool IsTraditional => string.Equals(StoryBy.Trim(), Traditional, StringComparison.OrdinalIgnoreCase);

    /// <summary>The names in <see cref="StoryBy"/>, trimmed, in order.</summary>
    public IReadOnlyList<string> Names() => StoryBy.Split(NameSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>A name as the language writes it when the book has a spelling for it; the name as written otherwise.</summary>
    public string NameIn(string name, string languageCode)
    {
        var primary = DinoBook.Primary(languageCode);
        var table = NamesByLanguage?.FirstOrDefault(pair => DinoBook.Primary(pair.Key) == primary).Value;
        return table is not null && table.TryGetValue(name, out var spelling) && !string.IsNullOrWhiteSpace(spelling) ? spelling : name;
    }
}

/// <summary>
/// A Dino book's back cover: the picture that prints as the book's last outside page, and the credits when the file had usable ones
/// (<see cref="Credits"/> is null when a field was missing or bad: only the credits are dropped, the picture still prints).
/// </summary>
public sealed record DinoBackCover(string PictureFile, DinoCredits? Credits);

/// <summary>Reads the <c>backCover</c> object of a <c>story.json</c> page; the file is untrusted, so every field is length-checked and a bad one is dropped.</summary>
public static class DinoBackCoverReader
{
    /// <summary>The picture name older files give by the last page's image instead of a <c>picture</c> field.</summary>
    public const string DefaultPictureName = "back-cover.jpg";

    public const int MaxNameChars = 500;
    public const int MaxSpellingLanguages = 30;
    public const int MaxSpellingsPerLanguage = 30;
    public const int MaxSpellingChars = 200;

    private const int FirstMonth = 1;
    private const int LastMonth = 12;
    private const int MinYear = 1000;
    private const int MaxYear = 9999;
    private const int MonthParts = 2;

    /// <summary>The back cover of <paramref name="element"/> (the last page's <c>backCover</c>); null when it names no usable picture.</summary>
    public static DinoBackCover? Read(JsonElement element, string? pageImageFile, Func<string?, bool> isPicture)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var picture = TextOf(element, "picture", MaxNameChars) ?? (pageImageFile == DefaultPictureName ? pageImageFile : null);
        return isPicture(picture) ? new DinoBackCover(picture!, CreditsOf(element)) : null;
    }

    private static DinoCredits? CreditsOf(JsonElement element)
    {
        var storyBy = TextOf(element, "storyBy", MaxNameChars);
        if (storyBy is null || !TryParseMonth(TextOf(element, "month", MaxNameChars), out var year, out var month))
        {
            return null;
        }

        var retold = element.TryGetProperty("retold", out var flag) && flag.ValueKind == JsonValueKind.True;
        var illustration = TextOf(element, "illustration", MaxNameChars) ?? DinoCredits.Maker;
        return new DinoCredits(storyBy, retold, illustration, year, month, SpellingsOf(element));
    }

    private static string? TextOf(JsonElement element, string name, int maxChars)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) || text.Length > maxChars ? null : text;
    }

    /// <summary>The per-language spellings of names; null (names as written) when the object is missing, malformed or over the caps.</summary>
    private static Dictionary<string, IReadOnlyDictionary<string, string>>? SpellingsOf(JsonElement element)
    {
        if (!element.TryGetProperty("namesByLanguage", out var all) || all.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var languages = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in all.EnumerateObject())
        {
            if (languages.Count >= MaxSpellingLanguages)
            {
                break;
            }

            if (language.Value.ValueKind == JsonValueKind.Object)
            {
                languages[language.Name] = SpellingsIn(language.Value);
            }
        }

        return languages.Count == 0 ? null : languages;
    }

    private static Dictionary<string, string> SpellingsIn(JsonElement language)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in language.EnumerateObject().Where(n => n.Value.ValueKind == JsonValueKind.String && n.Name.Length <= MaxSpellingChars))
        {
            if (names.Count >= MaxSpellingsPerLanguage)
            {
                break;
            }

            if (name.Value.GetString() is { Length: > 0 and <= MaxSpellingChars } spelling)
            {
                names[name.Name] = spelling;
            }
        }

        return names;
    }

    /// <summary>The year and month of <c>yyyy-MM</c> or of a full <c>yyyy-MM-dd</c> date.</summary>
    internal static bool TryParseMonth(string? text, out int year, out int month)
    {
        year = 0;
        month = 0;
        var parts = (text ?? string.Empty).Split('-');
        return parts.Length >= MonthParts
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out year)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out month)
            && year is >= MinYear and <= MaxYear
            && month is >= FirstMonth and <= LastMonth;
    }
}
