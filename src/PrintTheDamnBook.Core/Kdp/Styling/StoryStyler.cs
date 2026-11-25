using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>
/// Turns the story text of a book into styled pages (Slice 46). It takes the whole book because R7 (refrains) needs every page. Rules in the order the
/// design file gives (later wins for the property it sets): R2 sizes, R7 refrain, R8 shout, R9 stretch, R3 colour words, R4 triplets; R1 splits paragraphs.
/// R5 voices, R6 shapes and R10 songs (items E and F) add their steps to <see cref="StyleParagraph"/> and change <see cref="RunLook"/> values only.
/// A language without a lexicon skips R2, R3 and R9 quietly; R4, R7 and R8 need no word list.
/// </summary>
public sealed class StoryStyler
{
    private static readonly Rgb White = new(0xFF, 0xFF, 0xFF);

    private readonly IReadOnlyList<string> _texts;
    private readonly StoryLexicon? _lexicon;

    private readonly StoryVoices? _voices;
    private readonly IReadOnlyDictionary<string, string> _speakerColours;

    /// <param name="bookTexts">Every story page's text, in order (R7 needs the whole book).</param>
    /// <param name="language">The language of the texts; one without a lexicon skips the word-list rules quietly.</param>
    /// <param name="voices">The book's speakers (R5, R10, and the R7 exception for attributions); null skips voices and songs.</param>
    /// <param name="speakerColours">Each speaker's colour, from <see cref="SpeakerPalette.Resolve"/>; a speaker without one stays in ink.</param>
    public StoryStyler(IReadOnlyList<string> bookTexts, string language, StoryVoices? voices = null, IReadOnlyDictionary<string, string>? speakerColours = null)
    {
        _texts = bookTexts;
        _lexicon = StoryLexicon.For(language);
        _voices = voices;
        _speakerColours = speakerColours ?? new Dictionary<string, string>();
        Refrains = StoryRefrains.Find(bookTexts, voices);
    }

    /// <summary>The refrain clauses found in the book (keys: words lower-cased, joined by one space).</summary>
    public IReadOnlySet<string> Refrains { get; }

    /// <summary>The book's title with R3 only (colour words bold in their colour, darkened for <paramref name="tint"/>); the title page sets the sizes (<see cref="TitleLines"/>).</summary>
    public StyledParagraph StyleTitle(string title, Rgb? tint)
    {
        var tokens = StoryTokens.Split(title);
        var looks = tokens.Select(_ => RunLook.Body).ToArray();
        ApplyColourWords(tokens, looks, tint ?? White);
        return new StyledParagraph(GapLooks.Runs(tokens, looks));
    }

    /// <summary>The styled page for story index <paramref name="storyIndex"/>; <paramref name="tint"/> is the paper colour R3 darkens its colours against (null: white).</summary>
    public StyledPage Style(int storyIndex, Rgb? tint)
    {
        var background = tint ?? White;
        var paragraphs = _texts[storyIndex].Split('\n').Where(p => p.Length > 0).Select(p => StyleParagraph(p, background)).ToList();
        return new StyledPage(paragraphs);
    }

    private StyledParagraph StyleParagraph(string paragraph, Rgb background)
    {
        var tokens = StoryTokens.Split(paragraph);
        var looks = tokens.Select(_ => RunLook.Body).ToArray();
        var quotes = _voices is null ? [] : StoryQuotes.Find(tokens, _voices);
        ApplyVoices(quotes, looks);
        ApplySizes(tokens, looks);
        ApplyRefrains(tokens, looks);
        ApplyShouts(tokens, looks);
        ApplyStretch(tokens, looks);
        ApplySongs(quotes, looks);
        ApplyColourWords(tokens, looks, background);
        ApplyTriplets(tokens, looks);
        ApplyShapes(tokens, looks);
        return new StyledParagraph(GapLooks.Runs(tokens, looks));
    }

    /// <summary>R5: the words of a quote whose speaker has a colour take it; the narration around stays ink.</summary>
    private void ApplyVoices(IReadOnlyList<StoryQuote> quotes, RunLook[] looks)
    {
        foreach (var quote in quotes.Where(q => q.Speaker is not null && _speakerColours.ContainsKey(q.Speaker)))
        {
            foreach (var i in quote.Words)
            {
                looks[i] = looks[i] with { Color = _speakerColours[quote.Speaker!] };
            }
        }
    }

    /// <summary>R10: a quote attributed with sang or sings is 28 pt bold, in its singer's colour, its words alternately 2 pt above and 2 pt below the line.</summary>
    private static void ApplySongs(IReadOnlyList<StoryQuote> quotes, RunLook[] looks)
    {
        foreach (var quote in quotes.Where(q => q.IsSong))
        {
            for (var k = 0; k < quote.Words.Count; k++)
            {
                var up = k % 2 == 0;
                looks[quote.Words[k]] = looks[quote.Words[k]] with { Pt = StyleSpec.SongPt, Bold = true, Italic = false, BaselinePt = up ? StyleSpec.SongBouncePt : -StyleSpec.SongBouncePt, Song = true };
            }
        }
    }

    /// <summary>R6: a triplet whose word is in a meaning list is sized, moved or turned by its class. A move along the baseline is calibrated for 24 pt lines only, so it needs the triplet and its neighbouring words at that size.</summary>
    private void ApplyShapes(IReadOnlyList<StoryToken> tokens, RunLook[] looks)
    {
        if (_lexicon is null)
        {
            return;
        }

        var words = WordIndices(tokens);
        foreach (var triplet in StoryTriplets.Find(tokens))
        {
            var shape = StoryShapes.Classify(_lexicon, tokens, triplet);
            var at = new[] { triplet.First, triplet.Second, triplet.Third };
            if (shape == ShapeClass.None || (MovesAlongBaseline(shape) && !AllBodySize(words, at, looks)))
            {
                continue;
            }

            var moves = StoryShapes.MovesOf(shape);
            for (var n = 0; n < at.Length; n++)
            {
                looks[at[n]] = looks[at[n]] with { Pt = moves[n].Pt ?? looks[at[n]].Pt, BaselinePt = moves[n].BaselinePt, RotationDeg = moves[n].RotationDeg, SideMarginPt = moves[n].SideMarginPt };
            }
        }
    }

    private static bool MovesAlongBaseline(ShapeClass shape) => shape is ShapeClass.Down or ShapeClass.Up or ShapeClass.Bounce;

    private static bool AllBodySize(List<int> words, int[] triplet, RunLook[] looks)
    {
        var first = words.IndexOf(triplet[0]);
        var from = Math.Max(0, first - 1);
        var to = Math.Min(words.Count - 1, first + triplet.Length);
        return Enumerable.Range(from, to - from + 1).All(w => looks[words[w]].Pt == StyleSpec.ShiftLinePt && !looks[words[w]].Song);
    }

    /// <summary>R2: big words 30 pt bold, small words 19 pt, quiet words 19 pt italic in muted ink.</summary>
    private void ApplySizes(IReadOnlyList<StoryToken> tokens, RunLook[] looks)
    {
        if (_lexicon is null)
        {
            return;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].IsWord)
            {
                looks[i] = SizeLook(_lexicon, tokens[i].Key, looks[i]);
            }
        }

        ApplyBigPairs(tokens, looks, _lexicon);
    }

    private static RunLook SizeLook(StoryLexicon lexicon, string key, RunLook look)
    {
        if (lexicon.Big.Contains(key))
        {
            return look with { Pt = StyleSpec.BigPt, Bold = true };
        }

        if (lexicon.Small.Contains(key))
        {
            return look with { Pt = StyleSpec.SmallPt };
        }

        return lexicon.Quiet.Contains(key) ? look with { Pt = StyleSpec.SmallPt, Italic = true, Color = StyleSpec.MutedInk } : look;
    }

    private static void ApplyBigPairs(IReadOnlyList<StoryToken> tokens, RunLook[] looks, StoryLexicon lexicon)
    {
        var words = WordIndices(tokens);
        for (var w = 0; w + 1 < words.Count; w++)
        {
            var (a, b) = (words[w], words[w + 1]);
            if (lexicon.BigPairs.Any(p => p.First == tokens[a].Key && p.Second == tokens[b].Key))
            {
                looks[a] = looks[a] with { Pt = StyleSpec.BigPt, Bold = true };
                looks[b] = looks[b] with { Pt = StyleSpec.BigPt, Bold = true };
            }
        }
    }

    /// <summary>R7: every word of a clause that is a refrain of the book is 30 pt bold.</summary>
    private void ApplyRefrains(IReadOnlyList<StoryToken> tokens, RunLook[] looks)
    {
        foreach (var clause in StoryRefrains.Clauses(tokens).Where(c => Refrains.Contains(c.Key)))
        {
            for (var i = clause.FirstToken; i <= clause.LastToken; i++)
            {
                looks[i] = looks[i] with { Pt = StyleSpec.BigPt, Bold = true };
            }
        }
    }

    /// <summary>R8: the one to three words that start a quote and end in an exclamation mark are 30 pt bold.</summary>
    private static void ApplyShouts(IReadOnlyList<StoryToken> tokens, RunLook[] looks)
    {
        foreach (var i in StoryShouts.Find(tokens).SelectMany(s => s))
        {
            looks[i] = looks[i] with { Pt = StyleSpec.BigPt, Bold = true };
        }
    }

    /// <summary>R9: stretched words get 0.22 em of letter spacing.</summary>
    private void ApplyStretch(IReadOnlyList<StoryToken> tokens, RunLook[] looks)
    {
        if (_lexicon is null)
        {
            return;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].IsWord && _lexicon.Stretch.Contains(tokens[i].Key))
            {
                looks[i] = looks[i] with { LetterSpacingEm = StyleSpec.StretchEm };
            }
        }
    }

    /// <summary>R3: a colour word and the word after it (in the same clause) are bold in that colour, darkened until it reads on the page.</summary>
    private void ApplyColourWords(IReadOnlyList<StoryToken> tokens, RunLook[] looks, Rgb background)
    {
        if (_lexicon is null)
        {
            return;
        }

        var words = WordIndices(tokens);
        for (var w = 0; w < words.Count; w++)
        {
            if (!_lexicon.Colours.TryGetValue(tokens[words[w]].Key, out var baseColour))
            {
                continue;
            }

            var hex = StyleColors.ToHex(StyleColors.DarkenToContrast(baseColour, background));
            looks[words[w]] = looks[words[w]] with { Bold = true, Color = hex };
            if (w + 1 < words.Count && !StoryBoundaries.HasBoundary(Between(tokens, words[w], words[w + 1])))
            {
                looks[words[w + 1]] = looks[words[w + 1]] with { Bold = true, Color = hex };
            }
        }
    }

    /// <summary>R4: the three words of a triplet are bold, in the three triplet colours in order.</summary>
    private static void ApplyTriplets(IReadOnlyList<StoryToken> tokens, RunLook[] looks)
    {
        foreach (var (first, second, third) in StoryTriplets.Find(tokens))
        {
            var at = new[] { first, second, third };
            for (var n = 0; n < at.Length; n++)
            {
                looks[at[n]] = looks[at[n]] with { Bold = true, Color = StyleSpec.TripletColors[n] };
            }
        }
    }

    private static string Between(IReadOnlyList<StoryToken> tokens, int from, int to)
    {
        return StoryTokens.Join(tokens.Skip(from + 1).Take(to - from - 1));
    }

    private static List<int> WordIndices(IReadOnlyList<StoryToken> tokens)
    {
        return Enumerable.Range(0, tokens.Count).Where(i => tokens[i].IsWord).ToList();
    }
}

/// <summary>Gives the gaps between words their look and merges neighbouring tokens that look alike into runs.</summary>
internal static class GapLooks
{
    public static IReadOnlyList<StyledRun> Runs(IReadOnlyList<StoryToken> tokens, RunLook[] looks)
    {
        var pieces = new List<StyledRun>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].IsWord)
            {
                pieces.Add(new StyledRun(tokens[i].Text, looks[i]));
            }
            else
            {
                pieces.AddRange(Gap(tokens[i].Text, i > 0 ? looks[i - 1] : null, i + 1 < tokens.Count ? looks[i + 1] : null));
            }
        }

        return Merge(pieces);
    }

    /// <summary>
    /// A gap's marks stuck to the word before it (a comma, a closing quote) take that word's look and marks stuck to the word after it (an opening quote) that one's;
    /// the spaces between take the shared look when both words have one, else the plain body look.
    /// </summary>
    private static IEnumerable<StyledRun> Gap(string gap, RunLook? before, RunLook? after)
    {
        var firstSpace = gap.IndexOf(' ');
        var lastSpace = gap.LastIndexOf(' ');
        if (firstSpace < 0)
        {
            yield return new StyledRun(gap, before ?? after ?? RunLook.Body);
            yield break;
        }

        var head = gap[..firstSpace];
        var middle = gap[firstSpace..(lastSpace + 1)];
        var tail = gap[(lastSpace + 1)..];
        var shared = before is not null && before == after ? before : RunLook.Body;
        yield return new StyledRun(head, before ?? shared);
        yield return new StyledRun(middle, shared);
        yield return new StyledRun(tail, after ?? shared);
    }

    private static List<StyledRun> Merge(List<StyledRun> pieces)
    {
        var runs = new List<StyledRun>();
        foreach (var piece in pieces.Where(p => p.Text.Length > 0))
        {
            if (runs.Count > 0 && runs[^1].Look == piece.Look)
            {
                runs[^1] = runs[^1] with { Text = runs[^1].Text + piece.Text };
            }
            else
            {
                runs.Add(piece);
            }
        }

        return runs;
    }
}
