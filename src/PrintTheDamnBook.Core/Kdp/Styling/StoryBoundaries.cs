namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>Where a clause ends: a full stop, <c>! ? ; :</c> or a quote mark (R7, R3). Commas do not end one.</summary>
public static class StoryBoundaries
{
    private const string Marks = ".!?;:\"“”«»\n…";

    public static bool IsBoundary(char c) => Marks.Contains(c, StringComparison.Ordinal);

    public static bool HasBoundary(string gap) => gap.Any(IsBoundary);
}
