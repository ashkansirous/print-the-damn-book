using System.Security.Cryptography;
using System.Text;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>
/// The "More books" block of the last interior page (Slice 41): which other books are shown and how the tiles are laid out. The page never grows: at most
/// <see cref="MaxBooks"/> covers, picked from the author's other books by a shuffle seeded with this book's id, so a rebuild gives the same page.
/// </summary>
public static class KdpOtherBooks
{
    public const int MaxBooks = 15;
    public const int Columns = 5;

    public const double TileGapPt = 15;

    /// <summary>A full row must stay this far (points) inside the safe width, so rounding can never push it past.</summary>
    public const double RowSlackPt = 1;

    private const double PtPerInch = 72;

    /// <summary>The width of the safe area (inside the safe margin on both sides), in points.</summary>
    public static double SafeWidthPt => (KdpTrim.TrimInches - 2 * KdpTrim.SafeMarginInches) * PtPerInch;

    /// <summary>The cover tile is this wide and tall, in points: five tiles and their gaps fill the safe width less <see cref="RowSlackPt"/>.</summary>
    public static double TilePt => Math.Floor((SafeWidthPt - RowSlackPt - (Columns - 1) * TileGapPt) / Columns);

    /// <summary>The width of a full row of tiles, in points.</summary>
    public static double FullRowPt => Columns * TilePt + (Columns - 1) * TileGapPt;
    public const double HeadingPt = 22;

    /// <summary>The space between the heading, the grid and the Dino block, in points.</summary>
    public const double BlockGapPt = 28;

    /// <summary>The thin light-grey border round each tile (<c>#RRGGBB</c>) and its width in points.</summary>
    public const string TileBorderColor = "#C8C8C8";

    public const double TileBorderPt = 0.5;

    /// <summary>The other books in the seeded order, this book left out and each id once. The order does not depend on the order of <paramref name="candidates"/>.</summary>
    public static IReadOnlyList<T> Order<T>(string bookId, IEnumerable<T> candidates, Func<T, string> idOf)
    {
        return candidates
            .Where(c => !string.Equals(idOf(c), bookId, StringComparison.Ordinal))
            .GroupBy(idOf, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(c => Rank(bookId, idOf(c)), StringComparer.Ordinal)
            .ThenBy(idOf, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>At most <paramref name="max"/> of the other books, the first of <see cref="Order{T}"/>.</summary>
    public static IReadOnlyList<T> Pick<T>(string bookId, IEnumerable<T> candidates, Func<T, string> idOf, int max = MaxBooks)
    {
        return [.. Order(bookId, candidates, idOf).Take(Math.Max(0, max))];
    }

    /// <summary>The rows the tiles go in: <see cref="Columns"/> to a row, the last row may be shorter.</summary>
    public static IReadOnlyList<int> RowSizes(int tiles)
    {
        var rows = new List<int>();
        for (var left = tiles; left > 0; left -= Columns)
        {
            rows.Add(Math.Min(Columns, left));
        }

        return rows;
    }

    /// <summary>A stable pseudo-random key: the hex SHA-256 of the book's id and the candidate's id, so the shuffle is the same on every machine and run.</summary>
    private static string Rank(string bookId, string candidateId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(bookId + "|" + candidateId));
        return Convert.ToHexString(hash);
    }
}
