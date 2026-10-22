using System.Windows.Media;
using PrintTheDamnBook.Core;

namespace PrintTheDamnBook.App.ViewModels;

/// <summary>One page thumbnail with its label.</summary>
public sealed record PageViewModel(string Label, ImageSource? Image, PageKind Kind)
{
    /// <summary>The page in the editable list (strip only), used as its identity; null for blank padding, which cannot be edited.</summary>
    public BookPage? Source { get; init; }

    /// <summary>True for a cover that stays at its end of the book.</summary>
    public bool IsPinned { get; init; }

    public bool IsEditable => Source is not null;

    public bool IsBlank => Kind == PageKind.Blank;

    public bool IsUnrecognised => Kind is PageKind.Other or PageKind.DuplicateCover or PageKind.DuplicateBackCover;
}

public sealed record FaceViewModel(PageViewModel Left, PageViewModel Right);

public sealed record SheetViewModel(string Title, FaceViewModel Front, FaceViewModel Back);
