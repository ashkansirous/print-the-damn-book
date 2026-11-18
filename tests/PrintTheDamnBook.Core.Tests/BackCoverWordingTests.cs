using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Tests;

/// <summary>Slice 23: the back cover's credit lines in the print language, the same wording as Dino Reader.</summary>
public class BackCoverWordingTests
{
    private static DinoCredits Credits(string storyBy = "Aesop", bool retold = true, string illustration = "Ashkan Sirous", int year = 2026, int month = 11,
        Dictionary<string, Dictionary<string, string>>? spellings = null) =>
        new(storyBy, retold, illustration, year, month, spellings?.ToDictionary(p => p.Key, p => (IReadOnlyDictionary<string, string>)p.Value));

    [Fact]
    public void EnglishLinesAreInDinoReadersOrder()
    {
        var lines = BackCoverWording.Lines(Credits(), "en");

        Assert.Equal(["Original story by Aesop", "Retold for young readers by Ashkan Sirous", "Illustration: Ashkan Sirous", "November 2026"], lines);
    }

    [Fact]
    public void ANewStoryHasNoRetoldLineAndSaysStoryBy()
    {
        var lines = BackCoverWording.Lines(Credits("Elham Shafiei Gol", retold: false), "en");

        Assert.Equal(["Story by Elham Shafiei Gol", "Illustration: Ashkan Sirous", "November 2026"], lines);
    }

    [Fact]
    public void ATraditionalTaleHasItsOwnLine()
    {
        var lines = BackCoverWording.Lines(Credits("Traditional", retold: true), "de");

        Assert.Equal("Ein traditionelles Märchen", lines[0]);
        Assert.Equal("Für junge Leser nacherzählt von Ashkan Sirous", lines[1]);
    }

    [Fact]
    public void FarsiUsesFarsiMonthsAndDigits()
    {
        var lines = BackCoverWording.Lines(Credits("Aesop", retold: false), "fa");

        Assert.Equal("داستان از Aesop", lines[0]);
        Assert.Equal("نوامبر ۲۰۲۶", lines[^1]);
    }

    [Fact]
    public void GermanUsesGermanLabelsAndMonth()
    {
        var lines = BackCoverWording.Lines(Credits("Aesop", retold: false, month: 3), "de-DE");

        Assert.Equal(["Geschichte von Aesop", "Illustration: Ashkan Sirous", "März 2026"], lines);
    }

    [Fact]
    public void JapaneseAndSpanishPutTheYearWhereTheirLanguageDoes()
    {
        Assert.Equal("2026年11月", BackCoverWording.Lines(Credits(), "ja")[^1]);
        Assert.Equal("noviembre de 2026", BackCoverWording.Lines(Credits(), "es")[^1]);
    }

    [Theory]
    [InlineData("he")]
    [InlineData("xx")]
    [InlineData("")]
    [InlineData(null)]
    public void ALanguageWithoutWordingUsesEnglish(string? language)
    {
        Assert.Equal(BackCoverWording.Lines(Credits(), "en"), BackCoverWording.Lines(Credits(), language));
    }

    [Fact]
    public void TwoNamesAreJoinedWithTheLanguagesOwnAnd()
    {
        var credits = Credits("Elham Shafiei Gol; Ashkan Sirous", retold: false);

        Assert.Equal("Story by Elham Shafiei Gol and Ashkan Sirous", BackCoverWording.Lines(credits, "en")[0]);
        Assert.Equal("داستان از Elham Shafiei Gol و Ashkan Sirous", BackCoverWording.Lines(credits, "fa")[0]);
        Assert.Equal("Geschichte von Elham Shafiei Gol und Ashkan Sirous", BackCoverWording.Lines(credits, "de")[0]);
    }

    [Fact]
    public void ANameHoldingTheAndWordIsJoinedWithTheListSeparatorSoItNeverReadsAsThree()
    {
        var credits = Credits("Jacob et Wilhelm Grimm; Ashkan Sirous", retold: false);

        Assert.Equal("Histoire de Jacob et Wilhelm Grimm, Ashkan Sirous", BackCoverWording.Lines(credits, "fr")[0]);
    }

    [Fact]
    public void ABooksOwnSpellingOfANameIsUsedForThatLanguageOnly()
    {
        var credits = Credits("Aesop", retold: false, spellings: new() { ["fa"] = new() { ["Aesop"] = "ازوپ" } });

        Assert.Equal("داستان از ازوپ", BackCoverWording.Lines(credits, "fa")[0]);
        Assert.Equal("Story by Aesop", BackCoverWording.Lines(credits, "en")[0]);
    }

    [Fact]
    public void TextIsOneLineEachWithNoEmptyLines()
    {
        var text = BackCoverWording.Text(Credits(), "en");

        Assert.DoesNotContain("\n\n", text);
        Assert.Equal(4, text.Split('\n').Length);
    }

    [Fact]
    public void EveryLanguageInTheTableIsUsable()
    {
        foreach (var code in BackCoverWording.Languages)
        {
            Assert.True(BackCoverWording.IsUsable(BackCoverWording.WordsFor(code)), code);
            Assert.Equal(4, BackCoverWording.Lines(Credits(), code).Count);
        }

        Assert.Equal(15, BackCoverWording.Languages.Count);
    }

    [Fact]
    public void ABrokenEntryIsNotUsable()
    {
        var english = BackCoverWording.WordsFor("en");

        Assert.False(BackCoverWording.IsUsable(english with { Months = "a|b|c" }));
        Assert.False(BackCoverWording.IsUsable(english with { Digits = "012" }));
        Assert.False(BackCoverWording.IsUsable(english with { StoryBy = "no placeholder" }));
    }
}
