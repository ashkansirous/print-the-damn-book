using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests.Kdp;

public class KdpPagePlanTests
{
    [Theory]
    [InlineData(1, 24)]
    [InlineData(10, 26)]
    [InlineData(13, 32)]
    [InlineData(15, 36)]
    [InlineData(17, 40)]
    [InlineData(18, 42)]
    public void Page_count_follows_the_pictures_and_is_even_and_at_least_24(int pictures, int expected)
    {
        var plan = KdpPagePlan.Build(pictures, ReadingDirection.LeftToRight);

        Assert.Equal(expected, plan.PageCount);
        Assert.Equal(0, plan.PageCount % 2);
        Assert.True(plan.PageCount >= KdpPagePlan.MinPages);
    }

    [Theory]
    [InlineData(13, ReadingDirection.LeftToRight)]
    [InlineData(15, ReadingDirection.LeftToRight)]
    [InlineData(17, ReadingDirection.RightToLeft)]
    public void Every_text_page_faces_its_picture_in_one_spread(int pictures, ReadingDirection direction)
    {
        var plan = KdpPagePlan.Build(pictures, direction);

        for (var story = 0; story < pictures; story++)
        {
            var text = plan.Pages.Single(p => p.Role == KdpPageRole.StoryText && p.StoryIndex == story);
            var picture = plan.Pages.Single(p => p.Role == KdpPageRole.StoryPicture && p.StoryIndex == story);
            Assert.NotEqual(text.IsRightHand, picture.IsRightHand);
            Assert.Equal(1, Math.Abs(text.Position - picture.Position));
            Assert.Equal(0, Math.Min(text.Position, picture.Position) % 2);
        }
    }

    [Fact]
    public void Left_to_right_puts_the_words_on_the_left_and_the_picture_on_the_right()
    {
        var plan = KdpPagePlan.Build(13, ReadingDirection.LeftToRight);

        Assert.All(plan.Pages.Where(p => p.Role == KdpPageRole.StoryText), p => Assert.False(p.IsRightHand));
        Assert.All(plan.Pages.Where(p => p.Role == KdpPageRole.StoryPicture), p => Assert.True(p.IsRightHand));
    }

    [Fact]
    public void Right_to_left_mirrors_the_spread()
    {
        var plan = KdpPagePlan.Build(13, ReadingDirection.RightToLeft);

        Assert.All(plan.Pages.Where(p => p.Role == KdpPageRole.StoryText), p => Assert.True(p.IsRightHand));
        Assert.All(plan.Pages.Where(p => p.Role == KdpPageRole.StoryPicture), p => Assert.False(p.IsRightHand));
    }

    [Fact]
    public void The_front_matter_comes_first_and_the_title_is_a_right_hand_page()
    {
        var plan = KdpPagePlan.Build(13, ReadingDirection.LeftToRight);

        Assert.Equal([KdpPageRole.Title, KdpPageRole.Credits, KdpPageRole.BelongsTo], plan.Pages.Take(3).Select(p => p.Role));
        Assert.True(plan.Pages[0].IsRightHand);
    }

    [Fact]
    public void Blank_pages_sit_only_before_The_End_and_the_book_closes_with_The_End_and_the_Dino_page()
    {
        var plan = KdpPagePlan.Build(13, ReadingDirection.LeftToRight);

        var lastPicture = plan.Pages.Last(p => p.Role == KdpPageRole.StoryPicture).Position;
        var theEnd = plan.Pages.Single(p => p.Role == KdpPageRole.TheEnd).Position;
        Assert.All(plan.Pages.Where(p => p.Role == KdpPageRole.Blank), p => Assert.InRange(p.Position, lastPicture + 1, theEnd - 1));
        Assert.Equal(KdpPageRole.TheEnd, plan.Pages[^2].Role);
        Assert.Equal(KdpPageRole.Dino, plan.Pages[^1].Role);
        Assert.DoesNotContain(plan.Pages, p => p.Role == KdpPageRole.MoreBooks);
    }

    [Fact]
    public void The_more_books_page_sits_between_The_End_and_the_Dino_page_and_takes_a_blank_when_there_is_one()
    {
        var without = KdpPagePlan.Build(13, ReadingDirection.LeftToRight);
        var with = KdpPagePlan.Build(13, ReadingDirection.LeftToRight, moreBooks: true);

        Assert.Equal(without.PageCount, with.PageCount);
        Assert.Equal([KdpPageRole.TheEnd, KdpPageRole.MoreBooks, KdpPageRole.Dino], with.Pages.TakeLast(3).Select(p => p.Role));
        Assert.Equal(without.Pages.Count(p => p.Role == KdpPageRole.Blank) - 1, with.Pages.Count(p => p.Role == KdpPageRole.Blank));
    }

    [Theory]
    [InlineData(1, false, 24)]
    [InlineData(1, true, 24)]
    [InlineData(10, true, 26)]
    [InlineData(13, true, 32)]
    [InlineData(14, false, 34)]
    [InlineData(14, true, 34)]
    public void The_page_count_stays_even_and_at_least_24_with_or_without_more_books(int pictures, bool moreBooks, int expected)
    {
        var plan = KdpPagePlan.Build(pictures, ReadingDirection.LeftToRight, moreBooks);

        Assert.Equal(expected, plan.PageCount);
        Assert.Equal(expected, KdpPagePlan.TotalPages(pictures, moreBooks));
        Assert.Equal(0, plan.PageCount % 2);
    }

    [Fact]
    public void Positions_run_from_one_without_gaps()
    {
        var plan = KdpPagePlan.Build(15, ReadingDirection.LeftToRight);

        Assert.Equal(Enumerable.Range(1, plan.PageCount), plan.Pages.Select(p => p.Position));
    }

    [Fact]
    public void Only_text_pages_carry_a_number_and_it_is_their_position()
    {
        var plan = KdpPagePlan.Build(13, ReadingDirection.LeftToRight);

        Assert.All(plan.Pages.Where(p => p.Role != KdpPageRole.StoryText), p => Assert.Null(p.Number));
        Assert.All(plan.Pages.Where(p => p.Role == KdpPageRole.StoryText), p => Assert.Equal(p.Position, p.Number));
    }

    [Fact]
    public void Over_40_pages_warns_and_40_does_not()
    {
        Assert.Empty(KdpPagePlan.Build(17, ReadingDirection.LeftToRight).Warnings);
        Assert.Contains("flat-price", KdpPagePlan.Build(18, ReadingDirection.LeftToRight).Warnings.Single());
    }

    [Fact]
    public void A_book_without_pictures_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KdpPagePlan.Build(0, ReadingDirection.LeftToRight));
    }
}
