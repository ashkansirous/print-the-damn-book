using System.Windows.Media;
using PrintTheDamnBook.Core;

namespace PrintTheDamnBook.App.ViewModels;

/// <summary>One page thumbnail with its label.</summary>
public sealed record PageViewModel(string Label, ImageSource? Image, PageKind Kind)
{
    public bool IsBlank => Kind == PageKind.Blank;

    public bool IsUnrecognised => Kind is PageKind.Other or PageKind.DuplicateCover or PageKind.DuplicateBackCover;
}

public sealed record FaceViewModel(PageViewModel Left, PageViewModel Right);

public sealed record SheetViewModel(string Title, FaceViewModel Front, FaceViewModel Back);
