namespace PrintTheDamnBook.Core.Text;

/// <summary>Where a page's story text is printed.</summary>
public enum TextPlacement
{
    /// <summary>On a soft panel over the picture (the bottom band).</summary>
    OnPicture,

    /// <summary>On a page of its own, on the left, facing the picture on the right.</summary>
    OwnPage,
}

/// <summary>Which part of the picture the panel sits over (each band is at most 30% of the picture's height).</summary>
public enum TextBand
{
    Top,
    Middle,
    Bottom,
}

/// <summary>
/// The words that go with one picture page and where they are printed. <see cref="Band"/> is the user's pick (null until they
/// pick one); <see cref="Plainness"/> is what the app measured on the picture and is never saved.
/// </summary>
public sealed record StoryText(string Text, TextPlacement Placement = TextPlacement.OnPicture, TextBand? Band = null)
{
    /// <summary>How plain each band of the picture is, once the picture has been looked at; null until then.</summary>
    public BandPlainness? Plainness { get; init; }

    /// <summary>The plainest band, once measured; null until then.</summary>
    public TextBand? SuggestedBand => Plainness?.Suggested;

    /// <summary>The band the panel is printed in: the user's pick, else the suggestion, else the bottom.</summary>
    public TextBand EffectiveBand => Band ?? SuggestedBand ?? TextBand.Bottom;

    /// <summary>How plain the band in use is (0 busy to 1 plain), or null when the picture has not been measured.</summary>
    public double? EffectivePlainness => Plainness?.Of(EffectiveBand);

    /// <summary>True when there is nothing to print (empty or only white space).</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);

    /// <summary>The story when it has words, otherwise null.</summary>
    public static StoryText? OrNull(StoryText? story) => story is { IsEmpty: false } ? story : null;
}
