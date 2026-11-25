using System.Text.Json.Serialization;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>The main character chosen for a book and why (the style agent's one line).</summary>
public sealed record KdpMainCharacter(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("reason"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason = null);

/// <summary>The round title-page picture: a story picture and the circle cut from it, the centre as fractions of the picture's width and height, the diameter as a fraction of its shorter side (Slice 46 decision 9).</summary>
public sealed record KdpTitlePicture(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("centerX")] double CenterX,
    [property: JsonPropertyName("centerY")] double CenterY,
    [property: JsonPropertyName("diameter")] double Diameter)
{
    internal bool IsValid => Page >= 1 && CenterX is >= 0 and <= 1 && CenterY is >= 0 and <= 1 && Diameter is > 0 and <= 1;
}

/// <summary>
/// The per-book style choices in <c>kdp.json</c> (<c>styles</c>, Slice 46 decision 9): <c>speakers</c> (cast name to #RRGGBB), <c>mainCharacter</c> and <c>titlePicture</c>.
/// Every part is optional; with none saved the build uses the fallback palette, the speaker with most lines and the first story picture, and says so in the check report.
/// </summary>
public sealed record KdpStyleSettings(
    [property: JsonPropertyName("speakers"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, string>? Speakers = null,
    [property: JsonPropertyName("mainCharacter"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] KdpMainCharacter? MainCharacter = null,
    [property: JsonPropertyName("titlePicture"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] KdpTitlePicture? TitlePicture = null)
{
    public static KdpStyleSettings Empty { get; } = new();

    internal bool IsEmpty => (Speakers is null || Speakers.Count == 0) && MainCharacter is null && TitlePicture is null;

    /// <summary>The saved colour of each speaker (empty when none).</summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, string> SpeakerColours => Speakers ?? new Dictionary<string, string>();

    internal bool IsValid => (Speakers ?? []).All(s => s.Key.Length > 0 && KdpHexColor.TryNormalize(s.Value, out _))
        && (MainCharacter is null || !string.IsNullOrEmpty(MainCharacter.Name))
        && (TitlePicture?.IsValid ?? true);

    internal KdpStyleSettings Normalized()
    {
        var speakers = Speakers is { Count: > 0 } ? Speakers.ToDictionary(s => s.Key, s => KdpHexColor.TryNormalize(s.Value, out var hex) ? hex : s.Value, StringComparer.Ordinal) : null;
        return this with { Speakers = speakers };
    }
}
