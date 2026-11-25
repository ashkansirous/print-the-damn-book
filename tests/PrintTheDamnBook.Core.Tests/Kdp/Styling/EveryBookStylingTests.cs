using System.IO.Compression;
using System.Text.Json;
using PrintTheDamnBook.Core.Kdp.Styling;

namespace PrintTheDamnBook.Core.Tests.Kdp.Styling;

/// <summary>Opt-in (Slice 46 decision 14): reads every book in <c>D:\high-res-books</c> when the folder exists (not on CI) and checks styling never throws and never changes a word.</summary>
public class EveryBookStylingTests
{
    private const string BooksFolder = @"D:\high-res-books";

    [Fact]
    public void Every_local_book_styles_in_every_language_without_changing_its_words()
    {
        Assert.SkipUnless(Directory.Exists(BooksFolder), "no local books on this machine");
        var styled = 0;
        foreach (var file in Directory.EnumerateFiles(BooksFolder, "*.dino"))
        {
            styled += StyleBook(file);
        }

        Assert.True(styled > 0, "at least one page was styled");
    }

    private static int StyleBook(string file)
    {
        using var zip = ZipFile.OpenRead(file);
        using var json = JsonDocument.Parse(zip.GetEntry("story.json")!.Open());
        var root = json.RootElement;
        var count = 0;
        var cast = root.TryGetProperty("Cast", out var names) && names.ValueKind == JsonValueKind.Array
            ? names.EnumerateArray().Select(c => c.TryGetProperty("Name", out var n) ? n.GetString() ?? string.Empty : string.Empty).ToList()
            : [];
        foreach (var (language, texts) in TextsByLanguage(root))
        {
            var voices = StoryVoices.For(cast, language);
            var analysis = SpeakerAnalysis.Analyse(texts, voices);
            var colours = SpeakerPalette.Resolve(analysis.Speakers, new Dictionary<string, string>(), []).Colors;
            var styler = new StoryStyler(texts, language, voices, colours);
            for (var i = 0; i < texts.Count; i++)
            {
                Assert.Equal(texts[i], styler.Style(i, null).Text);
                count++;
            }
        }

        return count;
    }

    private static IEnumerable<(string Language, List<string> Texts)> TextsByLanguage(JsonElement root)
    {
        var source = root.GetProperty("Pages").EnumerateArray().Select(p => p.GetProperty("Text").GetString() ?? string.Empty).Select(Clean).ToList();
        yield return ("en", source);
        if (!root.TryGetProperty("Translations", out var translations) || translations.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var language in translations.EnumerateObject())
        {
            yield return (language.Name, source.Select(t => Translate(language.Value, t)).ToList());
        }
    }

    private static string Translate(JsonElement table, string text)
    {
        var lines = text.Split('\n').Select(line => string.Join(' ', System.Text.RegularExpressions.Regex.Split(line, @"(?<=[.!?])\s+").Select(s => table.TryGetProperty(s, out var t) ? t.GetString() ?? s : s)));
        return string.Join('\n', lines);
    }

    private static string Clean(string text) => PrintTheDamnBook.Core.Text.StoryLines.Clean(text);
}
