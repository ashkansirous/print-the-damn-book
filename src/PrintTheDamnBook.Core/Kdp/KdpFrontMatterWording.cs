using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>The words of the pages around the story (Slice 27, English only).</summary>
public sealed record KdpFrontMatter(
    string Title,
    IReadOnlyList<string> CopyrightLines,
    string DedicationHeading,
    string TheEnd,
    string AboutHeading,
    string AboutLine);

/// <summary>Where the title, copyright, dedication, end and about wording comes from; the Kdp project only draws it.</summary>
public static class KdpFrontMatterWording
{
    public const string English = "en";
    public const string DedicationHeading = "This book belongs to";
    public const string TheEnd = "The End";
    public const string AboutHeading = "Hear this story in";
    public const string AboutLine = "Dino: Kids Books & Stories";
    public const string AllRightsReserved = "All rights reserved.";

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
        return new KdpFrontMatter(title, lines, DedicationHeading, TheEnd, AboutHeading, AboutLine);
    }
}
