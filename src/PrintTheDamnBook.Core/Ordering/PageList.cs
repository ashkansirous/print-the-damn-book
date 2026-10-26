using System.Runtime.CompilerServices;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core;

/// <summary>
/// The editable list of real pages (never blanks) and the single source of truth for page
/// positions. The front cover and back cover stay pinned to the ends while dragging; blank
/// padding is derived by <see cref="Imposition.Pad"/> and is never part of this list.
/// Pages are identified by reference, so two pages with the same file stay distinct. A picture whose story text is
/// <see cref="TextPlacement.OwnPage"/> is expanded when padding: its text page goes on the page before it, and a spacer
/// goes in front of that when needed, so the text lands on a left page with its picture on the right of the same spread.
/// </summary>
public sealed class PageList
{
    private readonly List<BookPage> _pages;

    /// <summary>The text page of each picture, kept so a text page keeps its identity between calls.</summary>
    private readonly ConditionalWeakTable<BookPage, BookPage> _textPages = new();

    public PageList(IEnumerable<BookPage> pages)
    {
        _pages = pages.Where(p => p.Kind != PageKind.Blank).ToList();
    }

    /// <summary>A snapshot of the pages in order.</summary>
    public IReadOnlyList<BookPage> Pages => [.. _pages];

    /// <summary>The pages with text pages, spacers and blank padding added, as they will be imposed.</summary>
    public IReadOnlyList<BookPage> Padded => Imposition.Pad(Expanded());

    /// <summary>
    /// The real pages with each own-page story's text page (and the spacer that keeps it on a left page) added.
    /// Index 0 is the front cover, so a left page sits at an odd index; a text page that would land on an even one gets a spacer first.
    /// </summary>
    public IReadOnlyList<BookPage> Expanded()
    {
        var expanded = new List<BookPage>(_pages.Count);
        foreach (var page in _pages)
        {
            if (page.HasOwnPageText)
            {
                if (expanded.Count % 2 == 0)
                {
                    expanded.Add(BookPage.Spacer);
                }

                expanded.Add(TextPageOf(page));
            }

            expanded.Add(page);
        }

        return expanded;
    }

    /// <summary>The text page that faces <paramref name="picture"/>; the same instance every time, whose story is the picture's current one.</summary>
    private BookPage TextPageOf(BookPage picture)
    {
        return _textPages.GetValue(picture, p => new BookPage(null, p.Label, PageKind.Text) { Picture = p });
    }

    /// <summary>The picture that <paramref name="textPage"/> belongs to, or null when it is not a text page of this list.</summary>
    public BookPage? PictureOf(BookPage textPage) =>
        _pages.FirstOrDefault(p => p.HasOwnPageText && _textPages.TryGetValue(p, out var t) && ReferenceEquals(t, textPage));

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
