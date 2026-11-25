namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>
/// The title page's title (Slice 46, <c>docs/design/kdp-typography.md</c>): the last word on its own line at 72 pt, the rest at 36 pt; a one-word title is all 72 pt.
/// The runs keep what R3 gave them (bold, colour); only the size is set here.
/// </summary>
public sealed record TitleLines(IReadOnlyList<StyledRun> Head, IReadOnlyList<StyledRun> Last)
{
    public const double HeadPt = 36;
    public const double LastPt = 72;
    public const double LineHeight = 1.05;

    /// <summary>The words of the first line (empty for a one-word title).</summary>
    public string HeadText => string.Concat(Head.Select(r => r.Text));

    /// <summary>The words of the big last line.</summary>
    public string LastText => string.Concat(Last.Select(r => r.Text));

    public static TitleLines Split(StyledParagraph title)
    {
        var text = title.Text.TrimEnd();
        var cut = text.LastIndexOf(' ') + 1;
        var head = new List<StyledRun>();
        var last = new List<StyledRun>();
        var at = 0;
        foreach (var run in title.Runs)
        {
            var end = at + run.Text.Length;
            AddPart(head, run, at, at, Math.Min(end, cut), HeadPt);
            AddPart(last, run, at, Math.Max(at, cut), Math.Min(end, text.Length), LastPt);
            at = end;
        }

        return new TitleLines(TrimEnd(head), last);
    }

    /// <summary>The part of <paramref name="run"/> (which starts at <paramref name="runStart"/> in the title) between <paramref name="from"/> and <paramref name="to"/>, set at <paramref name="pt"/>.</summary>
    private static void AddPart(List<StyledRun> into, StyledRun run, int runStart, int from, int to, double pt)
    {
        if (to > from)
        {
            into.Add(new StyledRun(run.Text[(from - runStart)..(to - runStart)], run.Look with { Pt = pt }));
        }
    }

    private static List<StyledRun> TrimEnd(List<StyledRun> runs)
    {
        var trimmed = runs.ToList();
        while (trimmed.Count > 0 && trimmed[^1].Text.Trim().Length == 0)
        {
            trimmed.RemoveAt(trimmed.Count - 1);
        }

        if (trimmed.Count > 0)
        {
            trimmed[^1] = trimmed[^1] with { Text = trimmed[^1].Text.TrimEnd() };
        }

        return trimmed;
    }
}
