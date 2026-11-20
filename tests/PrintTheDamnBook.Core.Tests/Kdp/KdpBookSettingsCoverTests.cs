using PrintTheDamnBook.Core.Kdp;

namespace PrintTheDamnBook.Core.Tests.Kdp;

/// <summary>Slice 36 B: crop offsets of the two cover pictures in <c>kdp.json</c>, beside the story pages' (Slice 28).</summary>
public class KdpBookSettingsCoverTests
{
    [Fact]
    public void A_cover_offset_is_kept_per_cover_and_round_trips_through_the_file()
    {
        var settings = KdpBookSettings.Empty.WithOffset(7, 60).WithCoverOffset(KdpPictureKeys.Front, 30).WithCoverOffset(KdpPictureKeys.Back, 480).ForBook("id1", "Freddy");

        var read = KdpBookSettings.Parse(settings.ToJson());

        Assert.Equal((30, 480, 60), (read.CoverOffsetFor(KdpPictureKeys.Front), read.CoverOffsetFor(KdpPictureKeys.Back), read.OffsetFor(7)));
        Assert.Equal(["back", "front"], read.Covers);
        Assert.Equal("id1", read.BookId);
    }

    [Fact]
    public void A_file_from_before_covers_still_reads_and_has_no_cover_choice()
    {
        var read = KdpBookSettings.Parse("""{"pages":{"7":{"cropOffsetPx":60}}}""");

        Assert.Equal(60, read.OffsetFor(7));
        Assert.False(read.HasCoverOffset(KdpPictureKeys.Front));
        Assert.Empty(read.Covers);
    }

    [Fact]
    public void A_book_with_no_cover_choice_writes_no_covers_entry_so_older_readers_see_the_same_file()
    {
        var json = KdpBookSettings.Empty.WithOffset(2, 40).ToJson();

        Assert.DoesNotContain("covers", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cover_choice_of_zero_is_still_a_choice_and_removing_it_clears_it()
    {
        var chosen = KdpBookSettings.Empty.WithCoverOffset(KdpPictureKeys.Back, 0);

        Assert.True(chosen.HasCoverOffset(KdpPictureKeys.Back));
        Assert.False(chosen.WithoutCover(KdpPictureKeys.Back).HasCoverOffset(KdpPictureKeys.Back));
    }

    [Theory]
    [InlineData("""{"covers":{"spine":{"cropOffsetPx":5}}}""")]
    [InlineData("""{"covers":{"front":{"cropOffsetPx":-5}}}""")]
    [InlineData("""{"covers":{"front":null}}""")]
    public void A_bad_cover_entry_is_refused(string json)
    {
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Parse(json));
    }

    [Fact]
    public void Only_the_front_and_the_back_can_be_set()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KdpBookSettings.Empty.WithCoverOffset("spine", 5));
    }
}
