using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>The words of the pages around the story (Slice 27, English only).</summary>
public sealed record KdpFrontMatter(
    string Title,
    IReadOnlyList<string> CopyrightLines,
    string DedicationHeading,
    string TheEnd,
    string AboutLine,
    string OtherBooksHeading = KdpFrontMatterWording.OtherBooksHeadingText);

/// <summary>Where the title, copyright, dedication, end and about wording comes from; the Kdp project only draws it.</summary>
public static class KdpFrontMatterWording
{
    public const string English = "en";
    public const string DedicationHeading = "This book belongs to";
    public const string TheEnd = "The End";

    /// <summary>The Dino line, on the last interior page next to its QR code and on the back cover (Slice 37: one text for both).</summary>
    public const string DinoLine = "Listen to this story in Dino: Kids Books & Stories. Want to join in? Pick a character and speak their lines yourself!";
    public const string AllRightsReserved = "All rights reserved.";

    /// <summary>The heading above the other books' covers on the last page (Slice 41): "More books by Ashkan Sirous".</summary>
    public const string OtherBooksHeadingText = "More books by " + DinoCredits.Maker;

    /// <summary>
    /// The wording for a book. The credit lines come from <see cref="BackCoverWording"/> (English) when the book has credits; the copyright
    /// year is the credits' year, else <paramref name="fallbackYear"/>.
    /// </summary>
    public static KdpFrontMatter For(string title, DinoCredits? credits, int fallbackYear)
    {
        var lines = new List<string>();
        if (credits is not null)
        {
            lines.AddRange(BackCoverWording.Lines(credits, English));
        }

        var year = credits?.Year ?? fallbackYear;
        lines.Add($"© {year} {DinoCredits.Maker}. {AllRightsReserved}");
        return new KdpFrontMatter(title, lines, DedicationHeading, TheEnd, DinoLine);
    }
}
