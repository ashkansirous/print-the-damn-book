namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>
/// How one run of story text looks (Slice 46). Every rule changes a few of these; later items add values to the same shape without reshaping it:
/// R5 voices and R10 songs set <see cref="Color"/>, <see cref="Pt"/> and <see cref="BaselinePt"/>, R6 shapes set <see cref="Pt"/>, <see cref="BaselinePt"/>,
/// <see cref="RotationDeg"/> and <see cref="SideMarginPt"/>. A positive baseline is up.
/// </summary>
public sealed record RunLook(
    double Pt,
    bool Bold = false,
    bool Italic = false,
    string Color = StyleColors.Ink,
    double LetterSpacingEm = 0,
    double BaselinePt = 0,
    double RotationDeg = 0,
    double SideMarginPt = 0,
    bool Song = false)
{
    /// <summary>The body look: story size, regular, ink.</summary>
    public static RunLook Body { get; } = new(KdpTrim.StoryTextPt);

    /// <summary>True when the run has to be placed as its own inline element (a move, a turn or a side margin, or a word of a song, which bounces by its own padding): a plain span cannot do these.</summary>
    public bool NeedsInlineElement => BaselinePt != 0 || RotationDeg != 0 || SideMarginPt != 0 || Song;
}

/// <summary>Some text that is set with one look.</summary>
public sealed record StyledRun(string Text, RunLook Look);

/// <summary>One paragraph of a story page: its runs in reading order. Joining their texts gives the paragraph's words unchanged.</summary>
public sealed record StyledParagraph(IReadOnlyList<StyledRun> Runs)
{
    public string Text => string.Concat(Runs.Select(r => r.Text));
}

/// <summary>A story text page ready to draw: paragraphs set <see cref="StyleSpec.ParagraphGapPt"/> apart (R1).</summary>
public sealed record StyledPage(IReadOnlyList<StyledParagraph> Paragraphs)
{
    /// <summary>The page's words as the source had them: paragraphs joined by a single line break.</summary>
    public string Text => string.Join('\n', Paragraphs.Select(p => p.Text));

    /// <summary>A page with its text in one plain run per paragraph (no rule applied), e.g. for a language the rules cannot read.</summary>
    public static StyledPage Plain(string text)
    {
        var paragraphs = text.Split('\n').Select(p => new StyledParagraph([new StyledRun(p, RunLook.Body)])).ToList();
        return new StyledPage(paragraphs);
    }
}
