using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>The words of the pages around the story (Slice 27, English only).</summary>
public sealed record KdpFrontMatter(
    string Title,
    IReadOnlyList<string> CopyrightLines,
    string DedicationHeading,
    string TheEnd,
    string AboutLine,
    string OtherBooksHeading = KdpFrontMatterWording.OtherBooksHeadingText,
    IReadOnlyList<CreditRole>? CreditRoles = null,
    string DateLine = "",
    string RightsLine = "")
{
    /// <summary>The credit groups of the credits page (label and name); empty for a book with no credits.</summary>
    public IReadOnlyList<CreditRole> Roles => CreditRoles ?? [];
}

/// <summary>Where the title, copyright, dedication, end and about wording comes from; the Kdp project only draws it.</summary>
public static class KdpFrontMatterWording
{
    public const string English = "en";
    public const string DedicationHeading = "This book belongs to";
    public const string TheEnd = "The End";

    /// <summary>The Dino line, on the last interior page next to its QR code and on the back cover (Slice 37: one text for both).</summary>
    public const string DinoLine = ListenLine + " " + JoinBefore + JoinBold + JoinAfter;
    public const string AllRightsReserved = "All rights reserved.";

    /// <summary>The words of the Dino page (Slice 46, <c>docs/design/kdp-typography.md</c>); <see cref="DinoLine"/> is built from the listen and join parts, so they cannot drift.</summary>
    public const string ScanLine = "Scan the code, or type";

    public const string StoresLine = "Google Play · App Store · Microsoft Store";
    public const string ListenLine = "Listen to this story in Dino: Kids Books & Stories.";
    public const string JoinBefore = "Want to join in? Pick a character and ";
    public const string JoinBold = "speak their lines";
    public const string JoinAfter = " yourself!";

    /// <summary>The "Draw yourself here!" label in the box of the belongs-to page.</summary>
    public const string DrawLabel = "Draw yourself here!";

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
        var rights = $"© {year} {DinoCredits.Maker}. {AllRightsReserved}";
        lines.Add(rights);
        var roles = credits is null ? [] : BackCoverWording.Roles(credits, English);
        var date = credits is null ? "" : BackCoverWording.DateText(credits, English);
        return new KdpFrontMatter(title, lines, DedicationHeading, TheEnd, DinoLine, OtherBooksHeadingText, roles, date, rights);
    }
}
