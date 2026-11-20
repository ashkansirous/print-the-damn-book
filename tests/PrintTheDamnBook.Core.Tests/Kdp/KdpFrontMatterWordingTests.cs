using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;

namespace PrintTheDamnBook.Core.Tests.Kdp;

public class KdpFrontMatterWordingTests
{
    private static readonly DinoCredits Credits = new("Hans Christian Andersen", Retold: true, Illustration: "Ashkan Sirous", Year: 2026, Month: 11);

    [Fact]
    public void The_copyright_page_has_the_credit_lines_then_the_copyright_line_with_the_credits_year()
    {
        var wording = KdpFrontMatterWording.For("Freddy and the Slipper", Credits, fallbackYear: 2000);

        Assert.Equal("Freddy and the Slipper", wording.Title);
        Assert.Equal(BackCoverWording.Lines(Credits, "en").Count + 1, wording.CopyrightLines.Count);
        Assert.Equal("© 2026 Ashkan Sirous. All rights reserved.", wording.CopyrightLines[^1]);
    }

    [Fact]
    public void Without_credits_only_the_copyright_line_with_the_fallback_year_is_left()
    {
        var wording = KdpFrontMatterWording.For("T", null, fallbackYear: 2031);

        Assert.Equal(["© 2031 Ashkan Sirous. All rights reserved."], wording.CopyrightLines);
    }

    [Fact]
    public void The_fixed_pages_say_what_the_plan_names()
    {
        var wording = KdpFrontMatterWording.For("T", null, 2026);

        Assert.Equal("This book belongs to", wording.DedicationHeading);
        Assert.Equal("The End", wording.TheEnd);
        Assert.StartsWith("Listen to this story in Dino: Kids Books & Stories.", wording.AboutLine, StringComparison.Ordinal);
    }
}
