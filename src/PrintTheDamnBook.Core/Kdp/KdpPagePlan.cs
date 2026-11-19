using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Kdp;

public enum KdpPageRole
{
    Title,
    Copyright,
    Dedication,
    StoryText,
    StoryPicture,
    Blank,
    TheEnd,
    About,
}

/// <summary>
/// One page of the KDP interior. <see cref="Position"/> is its place in the book (the title page is 1); an odd position is a right-hand
/// page. <see cref="StoryIndex"/> is the Dino page a text or picture page belongs to; <see cref="Number"/> is the printed number
/// (text pages only).
/// </summary>
public sealed record KdpPage(int Position, KdpPageRole Role, int? StoryIndex, int? Number)
{
    public bool IsRightHand => Position % 2 == 1;
}

/// <summary>The pages of a KDP interior in order, and what to tell the owner about them.</summary>
public sealed record KdpPlan(IReadOnlyList<KdpPage> Pages, IReadOnlyList<string> Warnings)
{
    public int PageCount => Pages.Count;
}

/// <summary>
/// The page plan (Slice 27): title (right), copyright, dedication, then one spread per story page with the words on the left and the picture on
/// the right (mirrored for a right-to-left book), blank pages, The End, the about page. The count is even and at least 24; page numbers are the
/// physical position and only text pages carry one. (<see cref="Layout.PageNumbering.Compute"/> is not used: its Cover and Blank rules answer for a
/// saddle-stitch booklet, and here a printed book's number is simply its position.)
/// </summary>
public static class KdpPagePlan
{
    public const int MinPages = 24;

    /// <summary>Above this a paperback leaves KDP's lowest flat-price band.</summary>
    public const int FlatPriceMaxPages = 40;

    public const int KdpMaxPages = 828;

    private const int FrontMatterPages = 3;
    private const int BackMatterPages = 2;

    public static KdpPlan Build(int pictureCount, ReadingDirection direction)
    {
        if (pictureCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pictureCount), "A book needs at least one picture.");
        }

        var total = TotalPages(pictureCount);
        var roles = new List<(KdpPageRole Role, int? Story)>
        {
            (KdpPageRole.Title, null),
            (KdpPageRole.Copyright, null),
            (KdpPageRole.Dedication, null),
        };
        for (var i = 0; i < pictureCount; i++)
        {
            var textFirst = direction == ReadingDirection.LeftToRight;
            roles.Add(textFirst ? (KdpPageRole.StoryText, i) : (KdpPageRole.StoryPicture, i));
            roles.Add(textFirst ? (KdpPageRole.StoryPicture, i) : (KdpPageRole.StoryText, i));
        }

        var blanks = total - roles.Count - BackMatterPages;
        roles.AddRange(Enumerable.Repeat((KdpPageRole.Blank, (int?)null), blanks));
        roles.Add((KdpPageRole.TheEnd, null));
        roles.Add((KdpPageRole.About, null));

        var pages = roles.Select((r, i) => new KdpPage(i + 1, r.Role, r.Story, r.Role == KdpPageRole.StoryText ? i + 1 : null)).ToList();
        return new KdpPlan(pages, WarningsFor(total));
    }

    /// <summary>The page count: the pictures' spreads and the matter, rounded up to even, never under <see cref="MinPages"/>.</summary>
    public static int TotalPages(int pictureCount)
    {
        var needed = FrontMatterPages + 2 * pictureCount + BackMatterPages;
        var even = needed + needed % 2;
        return Math.Max(MinPages, even);
    }

    private static List<string> WarningsFor(int total)
    {
        var warnings = new List<string>();
        if (total > KdpMaxPages)
        {
            warnings.Add($"{total} pages is more than KDP's {KdpMaxPages}-page paperback limit.");
        }
        else if (total > FlatPriceMaxPages)
        {
            warnings.Add($"{total} pages is over {FlatPriceMaxPages}: the book leaves KDP's lowest flat-price band.");
        }

        return warnings;
    }
}
