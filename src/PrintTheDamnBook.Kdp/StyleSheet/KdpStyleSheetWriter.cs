using System.Globalization;
using System.Text;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Kdp.Styling;

namespace PrintTheDamnBook.Kdp.StyleSheet;

/// <summary>
/// <c>--style-sheet</c> (Slice 46 item H): <c>styles/speakers.md</c>, what the style agent needs to choose the per-book looks: each speaker with the number of lines, the story
/// pages they speak on and the colour in use, the main-character fallback and the tints of the story text pages (a speaker colour must read on all of them). The small
/// pictures it chooses the title picture from are the <c>crop/&lt;key&gt;.png</c> copies, which the same run writes. Never written without the option; an earlier run's file is removed.
/// </summary>
public static class KdpStyleSheetWriter
{
    public const string FolderName = "styles";
    public const string FileName = "speakers.md";

    public static string Write(string outFolder, KdpBookInput book, KdpInteriorResult result)
    {
        Remove(outFolder);
        var folder = Path.Combine(outFolder, FolderName);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName);
        File.WriteAllText(path, Build(book, result));
        return path;
    }

    /// <summary>Removes the style sheet file of an earlier run; the folder and anything else in it stay.</summary>
    public static void Remove(string outFolder)
    {
        var file = Path.Combine(outFolder, FolderName, FileName);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    internal static string Build(KdpBookInput book, KdpInteriorResult result)
    {
        var style = result.Style ?? Styling.KdpStyleOutcome.None;
        var analysis = SpeakerAnalysis.Analyse(book.Texts, style.Voices);
        var settings = book.Settings ?? KdpBookSettings.Empty;
        var text = new StringBuilder();
        text.AppendLine($"# Style sheet: {book.Title} ({book.Language})");
        text.AppendLine();
        text.AppendLine($"{book.Texts.Count} story pages. The pictures are `crop/<key>.png` (key = story page number), 480 px wide, whole and uncut.");
        text.AppendLine();
        AppendSpeakers(text, analysis, style);
        AppendMain(text, analysis, style, settings);
        AppendTints(text, result);
        AppendTitlePicture(text, settings, book.Texts.Count);
        return text.ToString();
    }

    private static void AppendSpeakers(StringBuilder text, SpeakerAnalysis analysis, Styling.KdpStyleOutcome style)
    {
        text.AppendLine("## Speakers");
        text.AppendLine();
        if (analysis.Speakers.Count == 0)
        {
            text.AppendLine("No speaker from the cast was found (the book has no cast, no attributed quote, or the language has no word list). Nothing to choose.");
            text.AppendLine();
            return;
        }

        text.AppendLine("Most lines first. Answer one line each: `<name> = #RRGGBB — reason`. A colour must be a dark shade (4.5:1 on white and on every tint below), not red, clearly different from the other speakers and from the three triplet colours "
            + string.Join(", ", StyleSpec.TripletColors) + ".");
        text.AppendLine();
        text.AppendLine("| Speaker | Lines | Story pages | Colour now |");
        text.AppendLine("| --- | --- | --- | --- |");
        foreach (var speaker in analysis.Speakers)
        {
            var colour = style.SpeakerColours.TryGetValue(speaker.Name, out var hex) ? hex : "none";
            text.AppendLine($"| {speaker.Name} | {speaker.Lines} | {string.Join(", ", speaker.Pages)} | {colour} |");
        }

        text.AppendLine();
        if (analysis.PagesWithUnknownSpeaker.Count > 0)
        {
            text.AppendLine($"A quote on story page(s) {string.Join(", ", analysis.PagesWithUnknownSpeaker)} has no speaker from the cast (a pronoun or no attribution); it stays in ink.");
            text.AppendLine();
        }
    }

    private static void AppendMain(StringBuilder text, SpeakerAnalysis analysis, Styling.KdpStyleOutcome style, KdpBookSettings settings)
    {
        text.AppendLine("## Main character");
        text.AppendLine();
        var fallback = analysis.MostLines?.Name ?? "none";
        var saved = settings.Styles.MainCharacter?.Name ?? "none";
        text.AppendLine($"In use: {style.MainCharacter ?? "none"}. Saved: {saved}. Fallback (most lines): {fallback}. The main character's colour is the colour of The End. Answer `main = <name> — reason`.");
        text.AppendLine();
    }

    private static void AppendTints(StringBuilder text, KdpInteriorResult result)
    {
        text.AppendLine("## Page tints");
        text.AppendLine();
        text.AppendLine("The paper colour of each story text page (the soft tint of the picture it faces); the first and the last are also the title page's and The End's.");
        text.AppendLine();
        text.AppendLine("| Story page | Tint |");
        text.AppendLine("| --- | --- |");
        for (var i = 0; i < result.Pictures.Count; i++)
        {
            var tint = result.Pictures[i].Tint is { } rgb ? StyleColors.ToHex(rgb) : "#FFFFFF";
            text.AppendLine($"| {i + 1} | {tint} |");
        }

        text.AppendLine();
    }

    private static void AppendTitlePicture(StringBuilder text, KdpBookSettings settings, int pageCount)
    {
        text.AppendLine("## Title picture");
        text.AppendLine();
        var saved = settings.Styles.TitlePicture;
        var now = saved is null
            ? "none saved (fallback: story page 1, centred, as wide as its shorter side)"
            : string.Create(CultureInfo.InvariantCulture, $"story page {saved.Page}, centre {saved.CenterX},{saved.CenterY}, diameter {saved.Diameter}");
        text.AppendLine($"In use: {now}. Choose the story page 1 to {pageCount} whose picture gives the best round portrait of the main character: face and body framed, a calm background, no other character cut in half.");
        text.AppendLine();
        text.AppendLine($"Answer `title = <page> <cx>,<cy>,<d> — reason`. Measure on that page's `crop/<page>.png` (the picture starts below a {KdpCropSheetLayout.DetailLabelHeightPx} px label strip): "
            + "cx = x of the circle's centre / 480, cy = (y of the centre − label strip) / the picture's height in the copy, d = the circle's diameter / 480 (the shorter side of a portrait picture is its width). "
            + "Each is a fraction between 0 and 1.");
    }
}
