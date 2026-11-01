using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests;

public class RightToLeftBookTests
{
    private const ReadingDirection Ltr = ReadingDirection.LeftToRight;
    private const ReadingDirection Rtl = ReadingDirection.RightToLeft;
    private const string Farsi = "سلام دنیا";
    private const string Hebrew = "שלום";

    private static List<BookPage> Book(int count)
    {
        var pages = new List<BookPage> { new("c.jpg", "Cover", PageKind.Cover) };
        pages.AddRange(Enumerable.Range(1, count - 2).Select(i => new BookPage($"{i}.jpg", i.ToString(), PageKind.Numbered)));
        pages.Add(new BookPage("b.jpg", "Back cover", PageKind.BackCover));
        return pages;
    }

    private static string Describe(Sheet[] sheets) =>
        string.Join(" / ", sheets.SelectMany(s => new[] { s.Front, s.Back }).Select(f => $"{f.Left.Label}+{f.Right.Label}"));

    [Fact]
    public void LeftToRight_IsTheDefaultAndUnchanged()
    {
        Assert.Equal(Describe(Imposition.Compute(Book(12))), Describe(Imposition.Compute(Book(12), Ltr)));
    }

    [Fact]
    public void FourPages_RightToLeft_PutsTheCoverOnTheLeftOfTheFront()
    {
        Assert.Equal("Cover+Back cover / 2+1", Describe(Imposition.Compute(Book(4), Rtl)));
    }

    [Fact]
    public void EightPages_RightToLeft()
    {
        Assert.Equal("Cover+Back cover / 6+1 / 2+5 / 4+3", Describe(Imposition.Compute(Book(8), Rtl)));
    }

    [Fact]
    public void TwelvePages_RightToLeft()
    {
        Assert.Equal("Cover+Back cover / 10+1 / 2+9 / 8+3 / 4+7 / 6+5", Describe(Imposition.Compute(Book(12), Rtl)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void RightToLeftSheet_IsTheLeftToRightSheetWithEachFacesSidesSwapped(int count)
    {
        var padded = Imposition.Pad(Book(count));

        var ltr = Imposition.Impose(padded, Ltr);
        var rtl = Imposition.Impose(padded, Rtl);

        Assert.Equal(ltr.Length, rtl.Length);
        for (var k = 0; k < ltr.Length; k++)
        {
            Assert.Same(ltr[k].Front.Left, rtl[k].Front.Right);
            Assert.Same(ltr[k].Front.Right, rtl[k].Front.Left);
            Assert.Same(ltr[k].Back.Left, rtl[k].Back.Right);
            Assert.Same(ltr[k].Back.Right, rtl[k].Back.Left);
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(12)]
    public void RightToLeft_FollowsThePlansFormula(int count)
    {
        var padded = Imposition.Pad(Book(count));
        var n = padded.Count;

        var sheets = Imposition.Impose(padded, Rtl);

        for (var k = 0; k < sheets.Length; k++)
        {
            Assert.Same(padded[2 * k], sheets[k].Front.Left);
            Assert.Same(padded[n - 1 - 2 * k], sheets[k].Front.Right);
            Assert.Same(padded[n - 2 - 2 * k], sheets[k].Back.Left);
            Assert.Same(padded[2 * k + 1], sheets[k].Back.Right);
        }
    }

    [Fact]
    public void RightToLeft_PadsBlanksBeforeTheBackCoverAsBefore()
    {
        var sheets = Imposition.Compute(Book(6), Rtl);

        Assert.Equal(2, sheets.Length);
        Assert.Equal("Cover", sheets[0].Front.Left.Label);
        Assert.Equal("Back cover", sheets[0].Front.Right.Label);
        Assert.Equal(PageKind.Blank, sheets[0].Back.Left.Kind);
    }

    [Fact]
    public void Spreads_RightToLeft_StartEachInsideSpreadOnItsRightPage()
    {
        var pages = Enumerable.Range(0, 8).Select(n => new BookPage($"{n}.jpg", n.ToString(), PageKind.Numbered)).ToList();

        var inside = Spreads.Of(pages, Rtl).Where(s => s.Kind == SpreadKind.Inside).ToList();

        Assert.Same(pages[1], inside[0].Right);
        Assert.Same(pages[2], inside[0].Left);
        Assert.Same(pages[5], inside[2].Right);
        Assert.Same(pages[6], inside[2].Left);
    }

    [Fact]
    public void Spreads_RightToLeft_KeepTheCoversWhereTheyAre()
    {
        var pages = Imposition.Pad(Book(8));

        var spreads = Spreads.Of(pages, Rtl);

        Assert.Same(pages[0], spreads[0].Right);
        Assert.Null(spreads[0].Left);
        Assert.Same(pages[^1], spreads[^1].Left);
        Assert.Null(spreads[^1].Right);
    }

    [Fact]
    public void OwnPageText_RightToLeft_SitsOnTheRightWithItsPictureOnTheLeft()
    {
        var list = new PageList(PageOrdering.FromFolder(["Cover.jpg", "Back cover.jpg", "1.jpg", "2.jpg", "3.jpg", "4.jpg", "5.jpg"]));
        list.Pages.First(p => p.Label == "2").Story = new StoryText("Words.", TextPlacement.OwnPage);

        var spread = Spreads.Of(list.Padded, Rtl).Single(s => s.Right?.Kind == PageKind.Text);

        Assert.Equal("2", spread.Left!.Label);
        Assert.Equal(PageKind.Text, spread.Right!.Kind);
    }

    [Fact]
    public void OwnPageText_RightToLeft_PrintsOnTheRightHalfOfItsFace()
    {
        var list = new PageList(PageOrdering.FromFolder(["Cover.jpg", "Back cover.jpg", "1.jpg", "2.jpg", "3.jpg", "4.jpg", "5.jpg"]));
        list.Pages.First(p => p.Label == "2").Story = new StoryText("Words.", TextPlacement.OwnPage);

        var sheets = Imposition.Impose(list.Padded, Rtl);
        var faces = sheets.SelectMany(s => new[] { s.Front, s.Back }).ToList();

        Assert.Single(faces, f => f.Right.Kind == PageKind.Text);
        Assert.DoesNotContain(faces, f => f.Left.Kind == PageKind.Text);
    }

    [Fact]
    public void ManualDuplex_RightToLeft_PlansTheSameFacesAsImposed()
    {
        var sheets = Imposition.Compute(Book(8), Rtl);

        var plan = ManualDuplexPlan.Build(sheets, new ManualDuplexOptions(ReverseBacks: false, RotateBacks: true));

        Assert.Equal(sheets.Select(s => s.Front), plan.Fronts.Select(f => f.Face));
        Assert.Equal(sheets.Select(s => s.Back), plan.Backs.Select(f => f.Face));
        Assert.All(plan.Backs, f => Assert.True(f.Rotate180));
    }

    [Theory]
    [InlineData("Hello", false)]
    [InlineData(Farsi, true)]
    [InlineData(Hebrew, true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("123 456", false)]
    [InlineData("... ?!", false)]
    [InlineData("۱۲۳", false)]
    public void StartsRightToLeft_UsesTheFirstStrongLetter(string? text, bool expected)
    {
        Assert.Equal(expected, TextDirection.StartsRightToLeft(text));
    }

    [Fact]
    public void StartsRightToLeft_SkipsLeadingPunctuationAndDigits()
    {
        Assert.True(TextDirection.StartsRightToLeft("\"1) " + Farsi));
        Assert.False(TextDirection.StartsRightToLeft("«1) Hello " + Farsi));
    }

    [Fact]
    public void StartsRightToLeft_MixedTextFollowsWhicheverLetterComesFirst()
    {
        Assert.True(TextDirection.StartsRightToLeft(Farsi + " hello"));
        Assert.False(TextDirection.StartsRightToLeft("hello " + Farsi));
    }

    [Theory]
    [InlineData("Hello", StoryFont.Andika)]
    [InlineData(Farsi, StoryFont.Vazirmatn)]
    [InlineData("hello " + Farsi, StoryFont.Vazirmatn)]
    [InlineData("123", StoryFont.Andika)]
    [InlineData("۱۲", StoryFont.Andika)]
    [InlineData("... ?!", StoryFont.Andika)]
    [InlineData(Hebrew, StoryFont.Andika)]
    [InlineData(null, StoryFont.Andika)]
    public void StoryFont_IsVazirmatnOnlyWhenTheTextHasArabicScriptLetters(string? text, string expected)
    {
        Assert.Equal(expected, StoryFont.For(text));
    }

    [Theory]
    [InlineData("fa", true)]
    [InlineData("fa-IR", true)]
    [InlineData("AR", true)]
    [InlineData("he", false)]
    [InlineData("en", false)]
    [InlineData("", false)]
    public void OnlyFarsiAndArabicAreSupportedRightToLeftLanguages(string code, bool expected)
    {
        Assert.Equal(expected, TextDirection.IsSupportedRightToLeftLanguage(code));
        Assert.Equal(expected ? Rtl : Ltr, TextDirection.ForLanguage(code));
    }

    [Fact]
    public void Resolve_APickAlwaysWins()
    {
        Assert.Equal(Ltr, TextDirection.Resolve(Ltr, "fa", [Farsi]));
        Assert.Equal(Rtl, TextDirection.Resolve(Rtl, null, ["Hello"]));
        Assert.Equal(Rtl, TextDirection.Resolve(Rtl, "en", [Farsi]));
    }

    [Fact]
    public void Resolve_ADinoBookFollowsItsLanguageNotItsText()
    {
        Assert.Equal(Rtl, TextDirection.Resolve(null, "fa", ["Hello"]));
        Assert.Equal(Ltr, TextDirection.Resolve(null, "de", [Farsi]));
    }

    [Fact]
    public void Resolve_AFolderBookIsGuessedFromItsFirstNonEmptyText()
    {
        Assert.Equal(Rtl, TextDirection.Resolve(null, null, [null, "  ", Farsi, "Hello"]));
        Assert.Equal(Ltr, TextDirection.Resolve(null, null, ["Hello", Farsi]));
        Assert.Equal(Ltr, TextDirection.Resolve(null, null, []));
    }

    [Fact]
    public void Resolve_ASavedPickIsNeverReplacedWhenTheTextChanges()
    {
        var saved = BookTextJson.Parse(BookTextJson.Serialize(new BookText { ReadingDirection = Ltr })).Book.ReadingDirection;

        Assert.Equal(Ltr, TextDirection.Resolve(saved, null, [Farsi]));
    }

    [Fact]
    public void BookText_ReadingDirectionRoundTrips()
    {
        var json = BookTextJson.Serialize(new BookText { ReadingDirection = Rtl });

        var load = BookTextJson.Parse(json);

        Assert.Contains("\"readingDirection\": \"RightToLeft\"", json);
        Assert.Equal(Rtl, load.Book.ReadingDirection);
        Assert.True(load.CanSave);
    }

    [Fact]
    public void BookText_WithoutAPick_WritesNoFieldAndReadsAsNoPick()
    {
        var json = BookTextJson.Serialize(new BookText());

        Assert.DoesNotContain("readingDirection", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(BookTextJson.Parse(json).Book.ReadingDirection);
    }

    [Fact]
    public void BookText_AnOldFileWithoutTheFieldStillLoads()
    {
        var load = BookTextJson.Parse("""{ "schemaVersion": 1, "textPages": "White", "pages": { "1.jpg": { "text": "Hi" } } }""");

        Assert.True(load.CanSave);
        Assert.Null(load.Book.ReadingDirection);
        Assert.Equal("Hi", load.Book.Pages["1.jpg"].Text);
    }

    [Theory]
    [InlineData("fa", PageNumberDigits.Persian)]
    [InlineData("fa-IR", PageNumberDigits.Persian)]
    [InlineData("ar", PageNumberDigits.ArabicIndic)]
    [InlineData("en", PageNumberDigits.Latin)]
    [InlineData("he", PageNumberDigits.Latin)]
    [InlineData(null, PageNumberDigits.Latin)]
    public void DigitsFollowTheBooksLanguage(string? code, PageNumberDigits expected)
    {
        Assert.Equal(expected, PageNumbering.DigitsFor(code));
    }

    [Theory]
    [InlineData(1, PageNumberDigits.Latin, "1")]
    [InlineData(10, PageNumberDigits.Latin, "10")]
    [InlineData(123, PageNumberDigits.Persian, "۱۲۳")]
    [InlineData(907, PageNumberDigits.Persian, "۹۰۷")]
    [InlineData(123, PageNumberDigits.ArabicIndic, "١٢٣")]
    [InlineData(40, PageNumberDigits.ArabicIndic, "٤٠")]
    public void Format_WritesTheNumberInTheGivenDigits(int number, PageNumberDigits digits, string expected)
    {
        Assert.Equal(expected, PageNumbering.Format(number, digits));
    }

    [Fact]
    public void Resolve_AHebrewFolderBookIsNotGuessedRightToLeft()
    {
        Assert.Equal(Ltr, TextDirection.Resolve(null, null, [Hebrew]));
        Assert.Equal(Rtl, TextDirection.Resolve(null, null, [Farsi]));
    }

    [Theory]
    [InlineData("پ", true)]
    [InlineData("چ", true)]
    [InlineData("ژ", true)]
    [InlineData("گ", true)]
    [InlineData("ک", true)]
    [InlineData("ی", true)]
    [InlineData("سلام", false)]
    [InlineData("Hello", false)]
    [InlineData(null, false)]
    public void HasPersianOnlyLetter_FindsTheLettersArabicSpellsDifferently(string? text, bool expected)
    {
        Assert.Equal(expected, TextDirection.HasPersianOnlyLetter(text));
    }

    [Fact]
    public void DigitsFor_AFolderBookFollowsItsScript()
    {
        Assert.Equal(PageNumberDigits.Latin, PageNumbering.DigitsFor(null, Ltr, [Farsi]));
        Assert.Equal(PageNumberDigits.Persian, PageNumbering.DigitsFor(null, Rtl, ["Hello", "گربه"]));
        Assert.Equal(PageNumberDigits.ArabicIndic, PageNumbering.DigitsFor(null, Rtl, ["مرحبا بالعالم"]));
        Assert.Equal(PageNumberDigits.ArabicIndic, PageNumbering.DigitsFor(null, Rtl, []));
    }

    [Fact]
    public void DigitsFor_ADinoBookStillFollowsItsLanguage()
    {
        Assert.Equal(PageNumberDigits.Persian, PageNumbering.DigitsFor("fa", Rtl, []));
        Assert.Equal(PageNumberDigits.Latin, PageNumbering.DigitsFor("en", Rtl, ["گربه"]));
    }

    [Fact]
    public void Choice_ClickingTheOptionAlreadyShownIsAPickThatNothingReplaces()
    {
        var choice = new BookDirectionChoice();
        Assert.Equal(Rtl, choice.Resolve(null, [Farsi]));

        choice.Pick(Rtl);

        Assert.Equal(Rtl, choice.Picked);
        Assert.Equal(Rtl, choice.Resolve(null, ["Hello"]));
        Assert.Equal(Rtl, choice.Resolve("en", ["Hello"]));
    }

    [Fact]
    public void Choice_ChangingTheLanguageClearsThePick()
    {
        var choice = new BookDirectionChoice();
        choice.Pick(Ltr);

        choice.LanguageChanged();

        Assert.Null(choice.Picked);
        Assert.Equal(Rtl, choice.Resolve("fa", []));
    }

    [Fact]
    public void Choice_RestoresASavedPickAndReportsWhyItReadsTheWayItDoes()
    {
        var choice = new BookDirectionChoice();
        Assert.Equal(DirectionSource.Guessed, choice.Source(isDinoBook: false));
        Assert.Equal(DirectionSource.Language, choice.Source(isDinoBook: true));

        choice.Restore(Rtl);

        Assert.Equal(DirectionSource.Picked, choice.Source(isDinoBook: true));
        choice.Restore(null);
        Assert.Null(choice.Picked);
    }

    [Fact]
    public void ManualDuplex_RightToLeft_IsTheLeftToRightPlanWithEachFaceMirrored()
    {
        var options = new ManualDuplexOptions(ReverseBacks: true, RotateBacks: true);
        var ltr = ManualDuplexPlan.Build(Imposition.Compute(Book(12), Ltr), options);
        var rtl = ManualDuplexPlan.Build(Imposition.Compute(Book(12), Rtl), options);

        Assert.Equal(ltr.Fronts.Select(f => f.Face.Left.Label), rtl.Fronts.Select(f => f.Face.Right.Label));
        Assert.Equal(ltr.Backs.Select(f => f.Face.Right.Label), rtl.Backs.Select(f => f.Face.Left.Label));
        Assert.Equal(ltr.Backs.Select(f => f.Rotate180), rtl.Backs.Select(f => f.Rotate180));
    }

    [Fact]
    public void TextPageSide_FollowsTheDirectionTheBookIsImposedIn()
    {
        var sheet = new SizeD(297, 210);
        var list = new PageList(PageOrdering.FromFolder(["Cover.jpg", "Back cover.jpg", "1.jpg", "2.jpg", "3.jpg", "4.jpg", "5.jpg"]));
        list.Pages.First(p => p.Label == "2").Story = new StoryText("Words.", TextPlacement.OwnPage);

        foreach (var (direction, textOnRight) in new[] { (Ltr, false), (Rtl, true) })
        {
            var faces = Imposition.Impose(list.Padded, direction).SelectMany(s => new[] { s.Front, s.Back });
            var face = faces.Single(f => f.Left.Kind == PageKind.Text || f.Right.Kind == PageKind.Text);
            var rightHalf = face.Right.Kind == PageKind.Text;
            var area = StoryTextLayout.TextPage(sheet, rightHalf, 5, PageNumberPosition.Off);

            Assert.Equal(textOnRight, rightHalf);
            Assert.Equal(textOnRight, area.Text.X >= sheet.Width / 2);
        }
    }
}
