using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests.Kdp;

/// <summary>Slice 46 item G: the words and maths of the redesigned front and back pages.</summary>
public class KdpFrontPagesTests
{
    private static StyledParagraph Title(string title, Rgb? tint = null) => new StoryStyler([title], "en").StyleTitle(title, tint);

    [Fact]
    public void The_last_word_of_the_title_is_its_own_72_pt_line_and_the_rest_is_36_pt()
    {
        var lines = TitleLines.Split(Title("X Marks the Treasure!"));

        Assert.Equal("X Marks the", lines.HeadText);
        Assert.Equal("Treasure!", lines.LastText);
        Assert.All(lines.Head, r => Assert.Equal(36, r.Look.Pt));
        Assert.All(lines.Last, r => Assert.Equal(72, r.Look.Pt));
    }

    [Fact]
    public void A_one_word_title_is_all_72_pt_with_no_first_line()
    {
        var lines = TitleLines.Split(Title("Freddy"));

        Assert.Empty(lines.Head);
        Assert.Equal("Freddy", lines.LastText);
        Assert.All(lines.Last, r => Assert.Equal(72, r.Look.Pt));
    }

    [Fact]
    public void Splitting_never_loses_or_repeats_a_letter()
    {
        foreach (var title in new[] { "The Gingerbread Man", "Little Red Riding Hood", "The Ugly Duckling", "The Three Little Pigs", "X Marks the Treasure!" })
        {
            var lines = TitleLines.Split(Title(title));

            Assert.Equal(title.Replace(" ", string.Empty), (lines.HeadText + lines.LastText).Replace(" ", string.Empty));
        }
    }

    [Fact]
    public void R3_applies_to_a_colour_word_in_the_title_and_the_next_word_taking_the_colour_darkened_for_the_page()
    {
        var lines = TitleLines.Split(Title("The Red Hen Hunt", new Rgb(0xF6, 0xF2, 0xEC)));

        var red = lines.Head.Single(r => r.Text.Contains("Red", StringComparison.Ordinal));
        Assert.True(red.Look.Bold);
        Assert.NotEqual(StyleColors.Ink, red.Look.Color);
        var colour = StyleColors.Parse(red.Look.Color);
        Assert.True(StyleColors.Contrast(colour, new Rgb(0xF6, 0xF2, 0xEC)) >= StyleSpec.MinContrast - 0.01);
    }

    [Fact]
    public void The_circle_is_the_first_picture_centred_as_wide_as_its_shorter_side_when_nothing_is_saved()
    {
        var square = KdpTitleCircle.Square(1792, 2400, null, 13);

        Assert.Equal(new KdpSquare(0, 304, 1792), square);
        Assert.Equal(1, KdpTitleCircle.PageFor(null, 13));
    }

    [Fact]
    public void A_saved_circle_is_cut_where_it_says_and_always_stays_inside_the_picture()
    {
        var saved = new KdpTitlePicture(4, 0.5, 0.4, 0.8);
        var square = KdpTitleCircle.Square(1792, 2400, saved, 13);

        Assert.Equal(1434, square.Side);
        Assert.Equal(179, square.X);
        Assert.Equal(960 - 717, square.Y);
        Assert.Equal(4, KdpTitleCircle.PageFor(saved, 13));

        var corner = KdpTitleCircle.Square(1792, 2400, new KdpTitlePicture(2, 1, 1, 1), 13);
        Assert.Equal(new KdpSquare(0, 608, 1792), corner);
    }

    [Fact]
    public void A_saved_picture_the_book_does_not_have_falls_back_to_the_first_picture_centred()
    {
        var saved = new KdpTitlePicture(20, 0.1, 0.1, 0.5);

        Assert.Equal(1, KdpTitleCircle.PageFor(saved, 13));
        Assert.Equal(new KdpSquare(0, 304, 1792), KdpTitleCircle.Square(1792, 2400, saved, 13));
    }

    [Fact]
    public void The_credits_page_roles_carry_the_labels_from_the_credit_wording_and_the_names_from_the_credits()
    {
        var roles = BackCoverWording.Roles(new DinoCredits("Ashkan Sirous", false, "Ashkan Sirous", 2026, 10), "en");

        Assert.Equal([new CreditRole("Story by", "Ashkan Sirous"), new CreditRole("Illustrations by", "Ashkan Sirous")], roles);
        Assert.Equal("October 2026", BackCoverWording.DateText(new DinoCredits("A", false, "B", 2026, 10), "en"));
    }

    [Fact]
    public void A_retelling_has_an_original_story_group_and_a_retold_by_group_and_a_traditional_tale_has_no_label()
    {
        var retold = BackCoverWording.Roles(new DinoCredits("Hans Christian Andersen", true, "Ashkan Sirous", 2026, 3), "en");
        var traditional = BackCoverWording.Roles(new DinoCredits("traditional", true, "Ashkan Sirous", 2026, 3), "en");

        Assert.Equal(
            [new CreditRole("Original story by", "Hans Christian Andersen"), new CreditRole("Retold for young readers by", "Ashkan Sirous"), new CreditRole("Illustrations by", "Ashkan Sirous")],
            retold);
        Assert.Equal(new CreditRole(null, "A traditional tale"), traditional[0]);
    }

    [Fact]
    public void Only_english_has_credit_labels_and_another_language_uses_them_too()
    {
        Assert.True(BackCoverWording.HasLabels("en-GB"));
        Assert.False(BackCoverWording.HasLabels("fr"));
        Assert.Equal("Story by", BackCoverWording.LabelsFor("fr").StoryBy);
    }

    [Fact]
    public void The_dino_page_words_together_are_the_one_dino_line_of_the_back_cover()
    {
        var page = KdpFrontMatterWording.ListenLine + " " + KdpFrontMatterWording.JoinBefore + KdpFrontMatterWording.JoinBold + KdpFrontMatterWording.JoinAfter;

        Assert.Equal(KdpFrontMatterWording.DinoLine, page);
        Assert.Equal("dinoreader.sirous.uk", KdpCoverWording.DinoShortAddress);
        Assert.Equal("https://dinoreader.sirous.uk/", KdpCoverWording.DinoReaderUrl);
    }

    [Fact]
    public void The_front_matter_wording_has_the_roles_the_date_and_the_rights_line()
    {
        var matter = KdpFrontMatterWording.For("T", new DinoCredits("Ashkan Sirous", false, "Ashkan Sirous", 2026, 10), 2000);

        Assert.Equal(2, matter.Roles.Count);
        Assert.Equal("October 2026", matter.DateLine);
        Assert.Equal("© 2026 Elinsoft Ltd. All rights reserved.", matter.RightsLine);
        Assert.Empty(KdpFrontMatterWording.For("T", null, 2031).Roles);
    }

    [Fact]
    public void The_belongs_to_name_line_is_85_percent_of_the_frame_content()
    {
        Assert.Equal(612 - 72 - 6 - 64, KdpMatterSpec.FrameInnerWidthPt, 0.01);
        Assert.Equal(0.85 * KdpMatterSpec.FrameInnerWidthPt, KdpMatterSpec.NameLineWidthPt, 0.01);
    }
}
