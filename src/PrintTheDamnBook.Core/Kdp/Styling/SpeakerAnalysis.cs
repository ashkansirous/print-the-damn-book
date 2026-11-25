namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>One speaking character: how many quotes the book gives them and on which story pages (numbered from 1, as in <c>kdp.json</c>).</summary>
public sealed record SpeakerInfo(string Name, int Lines, IReadOnlyList<int> Pages);

/// <summary>
/// Who speaks in a book (R5, Slice 46 item E): every cast name with at least one attributed quote, the most talkative first (a tie keeps cast order),
/// and the story pages that hold a quote whose speaker could not be found (a pronoun, or no attribution).
/// </summary>
public sealed record SpeakerAnalysis(IReadOnlyList<SpeakerInfo> Speakers, IReadOnlyList<int> PagesWithUnknownSpeaker, bool HasSongs)
{
    public static SpeakerAnalysis None { get; } = new([], [], false);

    /// <summary>The speaker with the most lines (the main character unless the agent overrides it); null when nobody speaks.</summary>
    public SpeakerInfo? MostLines => Speakers.Count > 0 ? Speakers[0] : null;

    public static SpeakerAnalysis Analyse(IReadOnlyList<string> pageTexts, StoryVoices? voices)
    {
        if (voices is null)
        {
            return None;
        }

        var lines = voices.Cast.DistinctBy(n => n.Name).ToDictionary(n => n.Name, _ => new List<int>(), StringComparer.Ordinal);
        var unknown = new SortedSet<int>();
        var songs = false;
        for (var page = 0; page < pageTexts.Count; page++)
        {
            foreach (var paragraph in pageTexts[page].Split('\n').Where(p => p.Length > 0))
            {
                foreach (var quote in StoryQuotes.Find(StoryTokens.Split(paragraph), voices))
                {
                    songs |= quote.IsSong;
                    if (quote.Speaker is { } name)
                    {
                        lines[name].Add(page + 1);
                    }
                    else
                    {
                        unknown.Add(page + 1);
                    }
                }
            }
        }

        var order = voices.Cast.Select((n, i) => (n.Name, i)).DistinctBy(p => p.Name).ToDictionary(p => p.Name, p => p.i, StringComparer.Ordinal);
        var speakers = lines.Where(l => l.Value.Count > 0)
            .OrderByDescending(l => l.Value.Count).ThenBy(l => order[l.Key])
            .Select(l => new SpeakerInfo(l.Key, l.Value.Count, l.Value.Distinct().ToList()))
            .ToList();
        return new SpeakerAnalysis(speakers, unknown.ToList(), songs);
    }
}
