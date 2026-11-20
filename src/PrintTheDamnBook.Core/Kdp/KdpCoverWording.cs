using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>The words the cover adds around the pictures (Slice 36); the Kdp project only draws them.</summary>
public static class KdpCoverWording
{
    /// <summary>The page the back cover's QR code opens.</summary>
    public const string DinoReaderUrl = "https://dinoreader.sirous.uk/";

    public const string WrittenBy = "Written by";
    public const string IllustratedBy = "Illustrated by";
    public const string CreditSeparator = " · ";
    public const string SpeakSuffix = ", and speak as the characters yourself!";

    private const string And = " and ";
    private const string List = ", ";
    private const int IllustrationFromEnd = 2;

    /// <summary>The line under the front title: "Written by X · Illustrated by Y" (a traditional tale has no writer, so only the illustrator); null without credits.</summary>
    public static string? FrontCredit(DinoCredits? credits, string? language)
    {
        if (credits is null)
        {
            return null;
        }

        if (DinoBook.Primary(language) != KdpFrontMatterWording.English)
        {
            return OtherLanguage(credits, language);
        }

        var illustrator = $"{IllustratedBy} {credits.Illustration}";
        if (credits.IsTraditional || credits.Names().Count == 0)
        {
            return illustrator;
        }

        var story = credits.Retold ? BackCoverWording.Lines(credits, language)[0] : $"{WrittenBy} {JoinNames(credits.Names())}";
        return $"{story}{CreditSeparator}{illustrator}";
    }

    /// <summary>The line next to the QR code: the Dino app's name as the about page words it, then the invitation to speak as the characters.</summary>
    public static string DinoLine => $"{KdpFrontMatterWording.AboutHeading} {KdpFrontMatterWording.AboutLine}{SpeakSuffix}";

    /// <summary>A language with its own back-cover wording keeps its own words: that language's "story by" line and illustration line, joined.</summary>
    private static string OtherLanguage(DinoCredits credits, string? language)
    {
        var lines = BackCoverWording.Lines(credits, language);
        var illustration = lines[^IllustrationFromEnd];
        return credits.IsTraditional ? illustration : $"{lines[0]}{CreditSeparator}{illustration}";
    }

    private static string JoinNames(IReadOnlyList<string> names)
    {
        return names.Count == 1 ? names[0] : $"{string.Join(List, names.Take(names.Count - 1))}{And}{names[^1]}";
    }
}
