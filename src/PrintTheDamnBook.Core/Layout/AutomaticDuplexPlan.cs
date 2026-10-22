namespace PrintTheDamnBook.Core.Layout;

/// <summary>The faces of an automatic two-sided job, front then back for each sheet.</summary>
public static class AutomaticDuplexPlan
{
    /// <summary>
    /// A long-edge flip turns the back of a landscape sheet 180 degrees relative to a short-edge flip, so the backs are
    /// drawn turned to cancel it. The page assignment never changes; only how the back is drawn.
    /// </summary>
    public static bool RotatesBacks(FlipEdge flip)
    {
        return flip == FlipEdge.LongEdge;
    }

    public static IReadOnlyList<PlannedFace> Build(IEnumerable<Sheet> sheets, FlipEdge flip)
    {
        var rotateBacks = RotatesBacks(flip);
        return sheets
            .SelectMany(s => new[] { new PlannedFace(s.Front, false, false), new PlannedFace(s.Back, rotateBacks, false) })
            .ToList();
    }
}
