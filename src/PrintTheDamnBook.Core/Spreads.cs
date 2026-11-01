using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core;

public enum SpreadKind
{
    FrontCover,
    Inside,
    BackCover,
}

/// <summary>
/// What the reading-order view shows in one row slot pair: the front cover sits alone on the right, the back cover
/// alone on the left, and the pages between them are paired as they face each other when the book is open.
/// <see cref="Number"/> counts the inside spreads from 1 and is 0 for the covers.
/// </summary>
public sealed record Spread(SpreadKind Kind, int Number, BookPage? Left, BookPage? Right);

public static class Spreads
{
    /// <summary>
    /// Groups pages already padded to a multiple of 4 (see <see cref="Imposition.Pad"/>) into spreads. In a right-to-left book each inside
    /// spread starts on its right page (the earlier page is on the right); the covers stay where they are.
    /// </summary>
    public static IReadOnlyList<Spread> Of(IReadOnlyList<BookPage> padded, ReadingDirection direction = ReadingDirection.LeftToRight)
    {
        if (padded.Count == 0)
        {
            return [];
        }

        var spreads = new List<Spread> { new(SpreadKind.FrontCover, 0, null, padded[0]) };
        for (var i = 1; i < padded.Count - 1; i += 2)
        {
            var (left, right) = direction == ReadingDirection.RightToLeft ? (padded[i + 1], padded[i]) : (padded[i], padded[i + 1]);
            spreads.Add(new Spread(SpreadKind.Inside, (i + 1) / 2, left, right));
        }

        if (padded.Count > 1)
        {
            spreads.Add(new Spread(SpreadKind.BackCover, 0, padded[^1], null));
        }

        return spreads;
    }
}
