using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests;

/// <summary>Slice 25: the cleaning that decides whether a page's words were changed.</summary>
public class StoryLinesTests
{
    [Theory]
    [InlineData("A.\n\nB.", "A.\nB.")]
    [InlineData("A.\r\n\r\nB.", "A.\nB.")]
    [InlineData("A.\rB.", "A.\nB.")]
    [InlineData("  A.  \n   \n  B.  ", "A.\nB.")]
    [InlineData("\n\nA.\n\n", "A.")]
    [InlineData("A.\nB.", "A.\nB.")]
    [InlineData("A. B.", "A. B.")]
    [InlineData("   \n\t\n", "")]
    [InlineData("", "")]
    public void CleanDropsBlankLinesAndTrimsEachLine(string text, string expected)
    {
        Assert.Equal(expected, StoryLines.Clean(text));
    }

    [Fact]
    public void CleanOfNullIsEmpty()
    {
        Assert.Equal(string.Empty, StoryLines.Clean(null));
    }

    [Fact]
    public void CleanKeepsSingleLineBreaksAndWordSpacing()
    {
        Assert.Equal("one  two\nthree", StoryLines.Clean("one  two\n\nthree"));
    }

    [Fact]
    public void SameWordsIgnoresOnlyBlankLinesAndLineEnds()
    {
        Assert.True(StoryLines.SameWords("A.\n\nB.", "A.\nB."));
        Assert.True(StoryLines.SameWords("A. \r\nB.", "A.\n\n B."));
        Assert.False(StoryLines.SameWords("A.\nB.", "A. B."));
        Assert.False(StoryLines.SameWords("A.", "A!"));
    }
}
