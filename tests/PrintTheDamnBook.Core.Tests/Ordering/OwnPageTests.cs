using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests;

public class OwnPageTests
{
    private static readonly StoryText Words = new("Once upon a time.", TextPlacement.OwnPage);

    private static PageList Book(int numbered, params int[] ownPages)
    {
        var list = new PageList(PageOrdering.FromFolder(["Cover.jpg", "Back cover.jpg", .. Enumerable.Range(1, numbered).Select(n => $"{n}.jpg")]));
        foreach (var number in ownPages)
        {
            list.Pages.First(p => p.Label == number.ToString()).Story = Words;
        }

        return list;
    }

    private static string[] Tags(IReadOnlyList<BookPage> pages) =>
        pages.Select(p => p.Kind switch
        {
            PageKind.Text => "T" + p.Label,
            PageKind.Spacer => "S",
            PageKind.Blank => "_",
            _ => p.Label,
        }).ToArray();

    [Fact]
    public void NoOwnPage_ChangesNothing()
    {
        Assert.Equal(["Cover", "1", "2", "3", "4", "5", "Back cover"], Tags(Book(5).Expanded()));
    }

    [Fact]
    public void OwnPageThatWouldLandOnTheRight_GetsASpacer()
    {
        // Cover is index 0 and "1" index 1 (left): the text of 2 would land at index 2 (a right page), so a spacer goes first.
        var tags = Tags(Book(4, 2).Expanded());

        Assert.Equal(["Cover", "1", "S", "T2", "2", "3", "4", "Back cover"], tags);
    }

    [Fact]
    public void OwnPageThatWouldLandOnTheLeft_NeedsNoSpacer()
    {
        var tags = Tags(Book(3, 1).Expanded());

        Assert.Equal(["Cover", "T1", "1", "2", "3", "Back cover"], tags);
    }

    [Fact]
    public void TextPageIsAlwaysOnAnOddIndex_AndItsPictureOnTheNextEvenOne()
    {
        var expanded = Book(9, 1, 2, 3, 5, 8).Expanded();

        for (var i = 0; i < expanded.Count; i++)
        {
            if (expanded[i].Kind == PageKind.Text)
            {
                Assert.Equal(1, i % 2);
                Assert.Equal(expanded[i].Label, expanded[i + 1].Label);
                Assert.NotEqual(PageKind.Text, expanded[i + 1].Kind);
            }
        }
    }

    [Fact]
    public void ConsecutiveOwnPages_NeedNoSpacerBetweenThem()
    {
        var tags = Tags(Book(3, 1, 2).Expanded());

        Assert.Equal(["Cover", "T1", "1", "T2", "2", "3", "Back cover"], tags);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void Padded_IsAMultipleOfFour_WithBlanksBeforeTheBackCover(int ownPages)
    {
        var list = Book(7, [.. Enumerable.Range(1, ownPages)]);

        var padded = list.Padded;

        Assert.Equal(0, padded.Count % 4);
        Assert.Equal(PageKind.BackCover, padded[^1].Kind);
        Assert.Equal(PageKind.Cover, padded[0].Kind);
        var blankAt = Enumerable.Range(0, padded.Count).Where(i => padded[i].Kind == PageKind.Blank).ToList();
        Assert.True(blankAt.Count < 4);
        Assert.Equal(Enumerable.Range(padded.Count - 1 - blankAt.Count, blankAt.Count), blankAt);
    }

    [Fact]
    public void InTheOpenBook_TheTextIsOnTheLeftAndThePictureOnTheRightOfTheSameSpread()
    {
        var list = Book(5, 2);

        var spreads = Spreads.Of(list.Padded);

        var spread = spreads.Single(s => s.Left?.Kind == PageKind.Text);
        Assert.Equal(SpreadKind.Inside, spread.Kind);
        Assert.Equal("2", spread.Right!.Label);
        Assert.NotEqual(PageKind.Text, spread.Right.Kind);
    }

    [Fact]
    public void WithoutABackCover_PaddingGoesAtTheEnd()
    {
        var list = new PageList(PageOrdering.FromFolder(["Cover.jpg", "1.jpg", "2.jpg", "3.jpg"]));
        list.Pages[2].Story = Words;

        var tags = Tags(list.Padded);

        Assert.Equal(["Cover", "1", "S", "T2", "2", "3", "_", "_"], tags);
    }

    [Fact]
    public void TextPage_ReadsTheStoryFromItsPicture_WithoutBeingExpandedAgain()
    {
        var list = Book(3, 2);
        var textPage = list.Expanded().Single(p => p.Kind == PageKind.Text);

        list.Pages.Single(p => p.Label == "2").Story = new StoryText("Edited later.", TextPlacement.OwnPage);

        Assert.Equal("Edited later.", textPage.Story!.Text);
    }

    [Fact]
    public void Pages_AreComparedByReference_SoEditingTheStoryNeverMovesTheirDictionaryKey()
    {
        var one = new BookPage("a.jpg", "a", PageKind.Added);
        var twin = new BookPage("a.jpg", "a", PageKind.Added);
        var numbers = new Dictionary<BookPage, int> { [one] = 1, [twin] = 2 };

        one.Story = new StoryText("Typed after the page was keyed.");

        Assert.NotEqual(one, twin);
        Assert.Equal(1, numbers[one]);
        Assert.Equal(2, numbers[twin]);
    }

    [Fact]
    public void TextPage_KeepsItsIdentityBetweenCalls_AndFollowsTheStory()
    {
        var list = Book(3, 2);
        var first = list.Expanded().Single(p => p.Kind == PageKind.Text);
        var picture = list.Pages.Single(p => p.Label == "2");

        picture.Story = new StoryText("New words.", TextPlacement.OwnPage);
        var second = list.Expanded().Single(p => p.Kind == PageKind.Text);

        Assert.Same(first, second);
        Assert.Equal("New words.", second.Story!.Text);
        Assert.Same(picture, list.PictureOf(second));
    }

    [Fact]
    public void MovingThePicture_MovesItsTextPage()
    {
        var list = Book(4, 2);

        Assert.True(list.Move(list.Pages[2], list.Pages[4]));

        var tags = Tags(list.Expanded());
        Assert.Equal(tags.IndexOf("T2") + 1, tags.IndexOf("2"));
    }

    [Fact]
    public void RemovingTheText_RemovesTheTextPage()
    {
        var list = Book(3, 2);
        list.Pages.Single(p => p.Label == "2").Story = new StoryText("", TextPlacement.OwnPage);

        Assert.DoesNotContain(list.Expanded(), p => p.Kind is PageKind.Text or PageKind.Spacer);
    }

    [Fact]
    public void CoversKeepTheirWordsOnThePicture()
    {
        var list = Book(3);
        list.Pages[0].Story = Words;
        list.Pages[^1].Story = Words;

        Assert.DoesNotContain(list.Expanded(), p => p.Kind is PageKind.Text or PageKind.Spacer);
        Assert.True(list.Pages[0].HasBandText);
        Assert.False(list.Pages[0].HasOwnPageText);
    }

    [Fact]
    public void PageNumbers_SkipSpacers_AndCountTextPages()
    {
        var padded = Book(3, 2).Padded;

        var numbers = PageNumbering.Compute(padded);

        Assert.Equal(PageKind.Spacer, padded[2].Kind);
        Assert.Equal(1, numbers[padded[1]]);
        Assert.False(numbers.ContainsKey(padded[2]));
        Assert.Equal(3, numbers[padded[3]]);
        Assert.Equal(4, numbers[padded[4]]);
    }

    [Fact]
    public void HasBandText_IsTrueOnlyForOnPictureWords()
    {
        var page = new BookPage("1.jpg", "1", PageKind.Numbered);
        Assert.False(page.HasBandText);

        page.Story = new StoryText("Hi");
        Assert.True(page.HasBandText);

        page.Story = new StoryText("Hi", TextPlacement.OwnPage);
        Assert.False(page.HasBandText);
        Assert.True(page.HasOwnPageText);

        page.Story = new StoryText("   ");
        Assert.False(page.HasBandText);
        Assert.False(page.HasOwnPageText);
    }
}
