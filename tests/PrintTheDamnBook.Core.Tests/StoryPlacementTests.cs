using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests;

/// <summary>Slice 23: words that do not fit the band move to their own page by themselves, unless the user chose.</summary>
public class StoryPlacementTests
{
    [Fact]
    public void WordsThatFitStayOnThePicture()
    {
        var resolved = new StoryPlacement(TextPlacement.OnPicture, false).Resolve(fitsBand: true);

        Assert.Equal(TextPlacement.OnPicture, resolved.Placement);
    }

    [Fact]
    public void WordsThatDoNotFitMoveToTheirOwnPage()
    {
        var resolved = new StoryPlacement(TextPlacement.OnPicture, false).Resolve(fitsBand: false);

        Assert.Equal(TextPlacement.OwnPage, resolved.Placement);
        Assert.False(resolved.ChosenByUser);
    }

    [Fact]
    public void APageMovedByItselfMovesBackWhenTheWordsFitAgain()
    {
        var moved = new StoryPlacement(TextPlacement.OnPicture, false).Resolve(fitsBand: false);

        Assert.Equal(TextPlacement.OnPicture, moved.Resolve(fitsBand: true).Placement);
    }

    [Fact]
    public void APageTheUserKeptOnThePictureIsNeverMoved()
    {
        var resolved = new StoryPlacement(TextPlacement.OnPicture, true).Resolve(fitsBand: false);

        Assert.Equal(TextPlacement.OnPicture, resolved.Placement);
        Assert.True(resolved.ChosenByUser);
    }

    [Fact]
    public void APageTheUserGaveItsOwnPageStaysThereEvenWhenTheWordsFit()
    {
        var resolved = new StoryPlacement(TextPlacement.OwnPage, true).Resolve(fitsBand: true);

        Assert.Equal(TextPlacement.OwnPage, resolved.Placement);
    }

    [Fact]
    public void FlippingTheSwitchMarksThePageAsChosen()
    {
        var chosen = new StoryPlacement(TextPlacement.OwnPage, false).ChosenAs(TextPlacement.OnPicture);

        Assert.Equal(new StoryPlacement(TextPlacement.OnPicture, true), chosen);
    }

    [Fact]
    public void ChosenByUserIsAlwaysSavedSoAMovedOwnPageIsNotReadBackAsChosen()
    {
        var book = new BookText { Pages = { ["1.jpg"] = new("a", TextPlacement.OwnPage, null, true), ["2.jpg"] = new("b", TextPlacement.OwnPage, null, false) } };

        var back = BookTextJson.Parse(BookTextJson.Serialize(book)).Book;

        Assert.True(back.Pages["1.jpg"].ChosenByUser);
        Assert.False(back.Pages["2.jpg"].ChosenByUser);
    }

    [Fact]
    public void AnEntryWithoutTheFieldReadsOwnPageAsChosenAndOnThePictureAsNotChosen()
    {
        var old = BookTextJson.Parse("""{"pages":{"3.jpg":{"text":"c","placement":"OwnPage"},"4.jpg":{"text":"d","placement":"OnPicture"},"5.jpg":{"text":"e"}}}""").Book;

        Assert.True(old.Pages["3.jpg"].ChosenByUser);
        Assert.False(old.Pages["4.jpg"].ChosenByUser);
        Assert.False(old.Pages["5.jpg"].ChosenByUser);
    }

    [Fact]
    public void BackCoverClearedIsSavedOnlyWhenTrue()
    {
        var plain = BookTextJson.Serialize(new BookText());
        var cleared = BookTextJson.Serialize(new BookText { BackCoverCleared = true });

        Assert.DoesNotContain("backCoverCleared", plain);
        Assert.True(BookTextJson.Parse(cleared).Book.BackCoverCleared);
    }

    [Fact]
    public void WordsAreTheTextWithoutEmptyLinesAndTheSavedTextIsUntouched()
    {
        var story = new StoryText("  One.  \n\n\nTwo.\r\n \r\nThree.");

        Assert.Equal("One.\nTwo.\nThree.", StoryLines.Clean(story.Text));
        Assert.Equal("  One.  \n\n\nTwo.\r\n \r\nThree.", story.Text);
    }

    [Fact]
    public void ATextOfOnlyEmptyLinesCountsAsNoText()
    {
        var story = new StoryText("\n \n\t\n");

        Assert.True(story.IsEmpty);
        Assert.Equal(string.Empty, StoryLines.Clean(story.Text));
    }
}
