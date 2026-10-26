namespace PrintTheDamnBook.Core.Text;

/// <summary>Where a page's story text is printed.</summary>
public enum TextPlacement
{
    /// <summary>On a soft panel over the picture (the bottom band).</summary>
    OnPicture,

    /// <summary>On a page of its own, on the left, facing the picture on the right.</summary>
    OwnPage,
}

/// <summary>The words that go with one picture page and where they are printed.</summary>
public sealed record StoryText(string Text, TextPlacement Placement = TextPlacement.OnPicture)
{
    /// <summary>True when there is nothing to print (empty or only white space).</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);

    /// <summary>The story when it has words, otherwise null.</summary>
    public static StoryText? OrNull(StoryText? story) => story is { IsEmpty: false } ? story : null;
}
