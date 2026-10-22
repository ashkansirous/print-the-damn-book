namespace PrintTheDamnBook.Core;

/// <summary>
/// The editable list of real pages (never blanks) and the single source of truth for page
/// positions. The front cover and back cover stay pinned to the ends while dragging; blank
/// padding is derived by <see cref="Imposition.Pad"/> and is never part of this list.
/// Pages are identified by reference, so two pages with the same file stay distinct.
/// </summary>
public sealed class PageList
{
    private readonly List<BookPage> _pages;

    public PageList(IEnumerable<BookPage> pages)
    {
        _pages = pages.Where(p => p.Kind != PageKind.Blank).ToList();
    }

    /// <summary>A snapshot of the pages in order.</summary>
    public IReadOnlyList<BookPage> Pages => [.. _pages];

    /// <summary>The pages with blank padding added, as they will be imposed.</summary>
    public IReadOnlyList<BookPage> Padded => Imposition.Pad(_pages);

    /// <summary>Moves the page at <paramref name="from"/> so it ends up at <paramref name="to"/>. Returns false (and changes nothing) if refused.</summary>
    public bool Move(int from, int to)
    {
        if (!InRange(from) || !InRange(to) || from == to || IsPinned(from) || IsPinned(to))
        {
            return false;
        }

        var page = _pages[from];
        _pages.RemoveAt(from);
        _pages.Insert(to, page);
        return true;
    }

    /// <summary>Moves <paramref name="page"/> to where <paramref name="target"/> is. Returns false if either is not in the list or the move is refused.</summary>
    public bool Move(BookPage page, BookPage target) => Move(IndexOf(page), IndexOf(target));

    /// <summary>Moves <paramref name="page"/> after the last real page (before the back cover when pinned). Returns false if refused.</summary>
    public bool MoveToEnd(BookPage page)
    {
        var last = HasBackCover() ? _pages.Count - 2 : _pages.Count - 1;
        return Move(IndexOf(page), last);
    }

    /// <summary>Removes the page at <paramref name="index"/>. Covers may be removed: only dragging is pinned.</summary>
    public bool Remove(int index)
    {
        if (!InRange(index))
        {
            return false;
        }

        _pages.RemoveAt(index);
        return true;
    }

    public bool Remove(BookPage page) => Remove(IndexOf(page));

    /// <summary>Appends files before the back cover (or at the end when there is none), labelled by file name.</summary>
    public void Add(IEnumerable<string> paths)
    {
        var at = HasBackCover() ? _pages.Count - 1 : _pages.Count;
        var added = paths.Select(path => new BookPage(path, Path.GetFileNameWithoutExtension(path), PageKind.Added));
        _pages.InsertRange(at, added);
    }

    public bool IsPinned(BookPage page) => IsPinned(IndexOf(page));

    public bool IsPinned(int index)
    {
        if (!InRange(index))
        {
            return false;
        }

        var isFront = index == 0 && _pages[index].Kind == PageKind.Cover;
        var isBack = index == _pages.Count - 1 && _pages[index].Kind == PageKind.BackCover;
        return isFront || isBack;
    }

    private int IndexOf(BookPage page) => _pages.FindIndex(p => ReferenceEquals(p, page));

    private bool HasBackCover() => _pages.Count > 0 && _pages[^1].Kind == PageKind.BackCover;

    private bool InRange(int index) => index >= 0 && index < _pages.Count;
}
