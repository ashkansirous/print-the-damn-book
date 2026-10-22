namespace PrintTheDamnBook.Core.Layout;

/// <summary>How the backs go back through a printer that cannot print two-sided.</summary>
/// <param name="ReverseBacks">Print the backs from the last sheet to the first.</param>
/// <param name="RotateBacks">Turn every back face 180 degrees on its sheet.</param>
/// <param name="MarkFeedEdge">Mark the feed edge on the fronts, for the test sheet.</param>
public sealed record ManualDuplexOptions(bool ReverseBacks = false, bool RotateBacks = false, bool MarkFeedEdge = false);

/// <summary>One face to print on its own (single-sided) page, and whether it is drawn turned 180 degrees.</summary>
public sealed record PlannedFace(SheetFace Face, bool Rotate180, bool MarksFeedEdge);

/// <summary>Two single-sided passes that together make the sheets two-sided: every front, then every back.</summary>
public sealed record ManualDuplexPlan(IReadOnlyList<PlannedFace> Fronts, IReadOnlyList<PlannedFace> Backs)
{
    /// <summary>
    /// Rotate-backs default. The reload instruction keeps the same physical edge leading in both passes, and the
    /// back of a short-edge-flipped sheet is then 180 degrees off, so the backs are turned unless the user says otherwise.
    /// </summary>
    public const bool DefaultRotateBacks = true;

    /// <summary>
    /// The sheet to print as the test sheet: the first whose back has two images, so a back that is upside down is
    /// visible; sheet 1 when none does. Null when there are no sheets.
    /// </summary>
    public static Sheet? PickTestSheet(IReadOnlyList<Sheet> sheets)
    {
        return sheets.FirstOrDefault(s => s.Back.Left.Path is not null && s.Back.Right.Path is not null) ?? sheets.FirstOrDefault();
    }

    public static ManualDuplexPlan Build(IReadOnlyList<Sheet> sheets, ManualDuplexOptions options)
    {
        var fronts = sheets.Select(s => new PlannedFace(s.Front, false, options.MarkFeedEdge)).ToList();
        var backs = sheets.Select(s => new PlannedFace(s.Back, options.RotateBacks, false)).ToList();
        if (options.ReverseBacks)
        {
            backs.Reverse();
        }

        return new ManualDuplexPlan(fronts, backs);
    }
}
