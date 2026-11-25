namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>The six R6 classes: how the three words of a triplet are moved or sized to show what they mean.</summary>
public enum ShapeClass
{
    None,
    Down,
    Up,
    Loud,
    Quiet,
    Shake,
    Bounce,
}

/// <summary>What a shape does to one word: a new size (null: unchanged), a baseline move (positive is up), a turn in degrees and a margin each side.</summary>
public sealed record ShapeMove(double? Pt = null, double BaselinePt = 0, double RotationDeg = 0, double SideMarginPt = 0);

/// <summary>R6 (Slice 46 item F): which class a triplet is, from the language's meaning lists, and the move of each of its three words (the table of <c>docs/design/kdp-typography.md</c>).</summary>
public static class StoryShapes
{
    private const double Body = 24;

    private static readonly IReadOnlyDictionary<ShapeClass, ShapeMove[]> Moves = new Dictionary<ShapeClass, ShapeMove[]>
    {
        [ShapeClass.Down] = [new(), new(BaselinePt: -3), new(BaselinePt: -6)],
        [ShapeClass.Up] = [new(), new(BaselinePt: 3), new(BaselinePt: 6)],
        [ShapeClass.Loud] = [new(Pt: Body), new(Pt: 27), new(Pt: 30)],
        [ShapeClass.Quiet] = [new(Pt: Body), new(Pt: 21), new(Pt: 19)],
        [ShapeClass.Shake] = [new(RotationDeg: -4, SideMarginPt: 2), new(RotationDeg: 3, SideMarginPt: 2), new(RotationDeg: -3, SideMarginPt: 2)],
        [ShapeClass.Bounce] = [new(BaselinePt: 3), new(BaselinePt: -2), new(BaselinePt: 2)],
    };

    /// <summary>The three moves of a class, first word to last.</summary>
    public static IReadOnlyList<ShapeMove> MovesOf(ShapeClass shape) => Moves[shape];

    /// <summary>
    /// The class of a triplet: the first of down, up, shake, bounce and sound whose list holds one of its three words; a sound is quiet when an R2 quiet word
    /// is in the same sentence or its attribution, else loud.
    /// </summary>
    public static ShapeClass Classify(StoryLexicon lexicon, IReadOnlyList<StoryToken> tokens, (int First, int Second, int Third) triplet)
    {
        var keys = new[] { triplet.First, triplet.Second, triplet.Third }.Select(i => tokens[i].Key).ToList();
        bool Has(IReadOnlySet<string> list) => keys.Any(list.Contains);
        if (Has(lexicon.Down))
        {
            return ShapeClass.Down;
        }

        if (Has(lexicon.Up))
        {
            return ShapeClass.Up;
        }

        if (Has(lexicon.Shake))
        {
            return ShapeClass.Shake;
        }

        if (Has(lexicon.Bounce))
        {
            return ShapeClass.Bounce;
        }

        if (!Has(lexicon.Sound))
        {
            return ShapeClass.None;
        }

        return HasQuietWord(lexicon, tokens, triplet) ? ShapeClass.Quiet : ShapeClass.Loud;
    }

    /// <summary>
    /// True when a quiet word sits in the triplet's sentence or in the attribution after its closing quote. Marks that end a sentence also end the search, except
    /// that a mark followed by a closing quote ends only the quote: <c>"Squawk, squawk, squawk," said Seagull softly.</c>
    /// </summary>
    private static bool HasQuietWord(StoryLexicon lexicon, IReadOnlyList<StoryToken> tokens, (int First, int Second, int Third) triplet)
    {
        var from = triplet.First;
        while (from > 0 && !EndsSentence(tokens[from - 1]))
        {
            from--;
        }

        var to = triplet.Third;
        while (to + 1 < tokens.Count && !(EndsSentence(tokens[to + 1]) && !ClosesQuote(tokens[to + 1])))
        {
            to++;
        }

        return Enumerable.Range(from, to - from + 1).Any(i => tokens[i].IsWord && lexicon.Quiet.Contains(tokens[i].Key));
    }

    private static bool EndsSentence(StoryToken token) => !token.IsWord && token.Text.IndexOfAny(['.', '!', '?']) >= 0;

    private static bool ClosesQuote(StoryToken token)
    {
        var last = token.Text.LastIndexOfAny(['.', '!', '?']);
        return token.Text[(last + 1)..].IndexOfAny(['"', '”']) >= 0;
    }
}

/// <summary>The maths that makes room for a shaped word, so no line touches the next (the drawing only applies it).</summary>
public static class ShapeMaths
{
    /// <summary>Average advance of a letter in Andika as a fraction of the font size (bold a little wider); a rough width for the padding, never for wrapping.</summary>
    private const double LetterWidthEm = 0.58;
    private const double BoldExtraEm = 0.04;

    /// <summary>The extra height a box of <paramref name="widthPt"/> by <paramref name="heightPt"/> needs above and below it once it is turned by <paramref name="degrees"/> about its centre.</summary>
    public static double RotationPaddingPt(double widthPt, double heightPt, double degrees)
    {
        var radians = Math.Abs(degrees) * Math.PI / 180;
        return Math.Max(0, (widthPt * Math.Sin(radians) + heightPt * Math.Cos(radians) - heightPt) / 2);
    }

    /// <summary>A rough width of <paramref name="text"/> set at <paramref name="pt"/>.</summary>
    public static double EstimateWidthPt(string text, double pt, bool bold) => text.Length * (LetterWidthEm + (bold ? BoldExtraEm : 0)) * pt;

    /// <summary>The padding a turned run gets: the box of its text at the page's line height, turned.</summary>
    public static double PaddingFor(RunLook look, string text, double lineHeight)
    {
        return RotationPaddingPt(EstimateWidthPt(text, look.Pt, look.Bold), look.Pt * lineHeight, look.RotationDeg);
    }
}
