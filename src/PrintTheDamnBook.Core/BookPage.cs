namespace PrintTheDamnBook.Core;

public enum PageKind
{
    Cover,
    Numbered,
    Other,
    BackCover,
    Blank,
    DuplicateCover,
    DuplicateBackCover,
}

/// <summary>One page of the book. <see cref="Path"/> is null for blank padding pages.</summary>
public sealed record BookPage(string? Path, string Label, PageKind Kind)
{
    public const string BlankLabel = "Blank";

    public static BookPage Blank { get; } = new(null, BlankLabel, PageKind.Blank);
}
