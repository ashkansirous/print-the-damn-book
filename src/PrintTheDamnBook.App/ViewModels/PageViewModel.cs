using System.ComponentModel;
using System.Windows.Media;
using PrintTheDamnBook.Core;

namespace PrintTheDamnBook.App.ViewModels;

/// <summary>One page thumbnail with its label.</summary>
public sealed record PageViewModel(string Label, ImageSource? Image, PageKind Kind) : INotifyPropertyChanged
{
    private const string BlankName = "Blank page";
    private const string SpacerName = "Spacer page";
    private const string BlankInTile = "BLANK";
    private const string SpacerInTile = "SPACER";

    private bool _isSelected;

    /// <summary>True while this tile is the picture (or the text page of the picture) being edited; set without rebuilding the tiles.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The page in the editable list (reading-order view only), used as its identity; null for blank padding, which cannot be edited. A text page carries its picture, so dragging or clicking it acts on the picture.</summary>
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

    /// <summary>The number printed on this page, or 0 when it has none (covers, blanks).</summary>
    public int PrintedNumber { get; init; }

    public bool HasPrintedNumber => PrintedNumber > 0;

    public bool IsEditable => Source is not null;

    /// <summary>True for the tile that can be removed from the book: a real picture, not its derived text page.</summary>
    public bool CanRemove => Source is not null && !IsText;

    public string AutomationName => Position > 0 ? $"{Label}, page {Position} of {Total}" : IsSpacer ? SpacerName : IsBlank ? BlankName : Label;

    /// <summary>Filler: shown as a dashed empty tile. A spacer is filler too.</summary>
    public bool IsBlank => Kind is PageKind.Blank or PageKind.Spacer;

    public bool IsSpacer => Kind == PageKind.Spacer;

    /// <summary>The story text of a picture that has its own page.</summary>
    public bool IsText => Kind == PageKind.Text;

    public string BlankTileText => IsSpacer ? SpacerInTile : BlankInTile;

    /// <summary>A picture or text page the user can click to edit its words.</summary>
    public bool IsSelectable => Source is not null;

    public bool HasNote => Note is not null;

    public bool ShowsWarnBadge => IsWarned && !IsBlank;

    public string ToolTip => Note ?? Label;
}

public sealed record FaceViewModel(PageViewModel Left, PageViewModel Right);

public sealed record SheetViewModel(string Title, bool IsFirst, FaceViewModel Front, FaceViewModel Back);
