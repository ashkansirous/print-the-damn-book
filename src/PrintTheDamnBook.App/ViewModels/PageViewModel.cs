using System.Windows.Media;
using PrintTheDamnBook.Core;

namespace PrintTheDamnBook.App.ViewModels;

/// <summary>One page thumbnail with its label.</summary>
public sealed record PageViewModel(string Label, ImageSource? Image, PageKind Kind)
{
    private const string BlankName = "Blank page";

    /// <summary>The page in the editable list (reading-order view only), used as its identity; null for blank padding, which cannot be edited.</summary>
    public BookPage? Source { get; init; }

    /// <summary>True for a cover that stays at its end of the book.</summary>
    public bool IsPinned { get; init; }

    /// <summary>What is wrong with this page, or a plain hint for filler pages; null when there is nothing to say.</summary>
    public string? Note { get; init; }

    /// <summary>True when the note is a problem: the tile gets the warning ring.</summary>
    public bool IsWarned { get; init; }

    /// <summary>1-based place among the real pages of the book (reading-order view); 0 when the tile has none.</summary>
    public int Position { get; init; }

    /// <summary>How many real pages the book has; with <see cref="Position"/> it names the tile for screen readers.</summary>
    public int Total { get; init; }

    public bool IsEditable => Source is not null;

    public string AutomationName => Position > 0 ? $"{Label}, page {Position} of {Total}" : IsBlank ? BlankName : Label;

    public bool IsBlank => Kind == PageKind.Blank;

    public bool HasNote => Note is not null;

    public bool ShowsWarnBadge => IsWarned && !IsBlank;

    public string ToolTip => Note ?? Label;
}

public sealed record FaceViewModel(PageViewModel Left, PageViewModel Right);

public sealed record SheetViewModel(string Title, bool IsFirst, FaceViewModel Front, FaceViewModel Back);
