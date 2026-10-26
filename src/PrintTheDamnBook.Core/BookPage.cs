using PrintTheDamnBook.Core.Text;

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
    /// <summary>Added by hand with "Add pages"; the user chose it, so it raises no warning.</summary>
    Added,

    /// <summary>The story text of a picture placed on a page of its own, facing the picture (derived by <see cref="PageList"/>, never edited).</summary>
    Text,

    /// <summary>A blank page that keeps a text page facing its picture (derived by <see cref="PageList"/>).</summary>
    Spacer,
}

/// <summary>One page of the book. <see cref="Path"/> is null for blank padding pages.</summary>
public sealed record BookPage(string? Path, string Label, PageKind Kind)
{
    public const string BlankLabel = "Blank";
    public const string SpacerLabel = "Spacer";

    private StoryText? _story;

    /// <summary>For a <see cref="PageKind.Text"/> page, the picture it carries the words of; null for every other page.</summary>
    public BookPage? Picture { get; init; }

    /// <summary>Pages are identified by reference (two pages of the same file stay distinct), and the mutable <see cref="Story"/> must never change a hash.</summary>
    public bool Equals(BookPage? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    /// <summary>
    /// The words of this page. Mutable on purpose: pages are identified by reference, so editing the text must not make a new page.
    /// On a <see cref="PageKind.Text"/> page it is read from its <see cref="Picture"/>, so it is never a stale copy.
    /// </summary>
    public StoryText? Story
    {
        get => Kind == PageKind.Text ? Picture?.Story : _story;
        set => _story = value;
    }

    /// <summary>Covers keep their words on the picture; blanks, spacers and text pages have no picture to face.</summary>
    public bool CanHaveOwnPage => Kind is not (PageKind.Cover or PageKind.BackCover or PageKind.Blank or PageKind.Text or PageKind.Spacer);

    /// <summary>Where the words are printed, or null when the page has none. A cover's <see cref="TextPlacement.OwnPage"/> reads as on the picture.</summary>
    public TextPlacement? EffectivePlacement => Story is { IsEmpty: false } story
        ? story.Placement == TextPlacement.OwnPage && CanHaveOwnPage ? TextPlacement.OwnPage : TextPlacement.OnPicture
        : null;

    /// <summary>True when the page has words printed over its picture.</summary>
    public bool HasBandText => Kind is not PageKind.Text && EffectivePlacement == TextPlacement.OnPicture;

    /// <summary>True when the page has words that go on a page of their own.</summary>
    public bool HasOwnPageText => EffectivePlacement == TextPlacement.OwnPage;

    public static BookPage Blank { get; } = new(null, BlankLabel, PageKind.Blank);

    public static BookPage Spacer { get; } = new(null, SpacerLabel, PageKind.Spacer);
}
