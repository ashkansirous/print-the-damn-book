using PrintTheDamnBook.Core.Kdp;

namespace PrintTheDamnBook.Core.Tests.Kdp;

public class KdpBookSettingsTests
{
    [Fact]
    public void An_offset_is_written_and_read_back()
    {
        var json = KdpBookSettings.Empty.WithOffset(7, 60).WithOffset(2, 0).ToJson();

        var read = KdpBookSettings.Parse(json);

        Assert.Equal(60, read.OffsetFor(7));
        Assert.True(read.HasOffset(2));
        Assert.Equal(0, read.OffsetFor(2));
        Assert.False(read.HasOffset(3));
        Assert.Contains("\"cropOffsetPx\": 60", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_documented_file_shape_is_read()
    {
        var read = KdpBookSettings.Parse("{ \"pages\": { \"7\": { \"cropOffsetPx\": 60 } } }");

        Assert.Equal(60, read.OffsetFor(7));
    }

    [Fact]
    public void A_negative_offset_is_saved_as_zero()
    {
        Assert.Equal(0, KdpBookSettings.Empty.WithOffset(1, -5).OffsetFor(1));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ \"pages\": { \"x\": { \"cropOffsetPx\": 1 } } }")]
    [InlineData("{ \"pages\": { \"0\": { \"cropOffsetPx\": 1 } } }")]
    [InlineData("{ \"pages\": { \"3\": { \"cropOffsetPx\": -1 } } }")]
    [InlineData("{ \"pages\": { \"3\": null } }")]
    [InlineData("{ \"pages\": { \"3\": {} } }")]
    [InlineData("null")]
    public void A_bad_file_is_refused(string json)
    {
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Parse(json));
    }

    [Fact]
    public void Without_clears_a_pages_choice_and_pages_lists_the_rest()
    {
        var settings = KdpBookSettings.Empty.WithOffset(7, 60).WithOffset(2, 10).Without(7);

        Assert.False(settings.HasOffset(7));
        Assert.Equal([2], settings.Pages);
    }

    [Fact]
    public void Writing_leaves_no_temporary_file_and_replaces_the_old_one()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ptdb-kdpset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, KdpBookSettings.FileName);
            KdpBookSettings.Empty.WithOffset(4, 25).Write(path);
            KdpBookSettings.Empty.WithOffset(4, 30).Write(path);

            Assert.Equal(30, KdpBookSettings.Read(path).OffsetFor(4));
            Assert.Equal([path], Directory.GetFiles(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_missing_file_means_no_choices_and_a_broken_one_is_left_alone()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ptdb-kdpset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, KdpBookSettings.FileName);
            Assert.False(KdpBookSettings.Read(path).HasOffset(1));

            File.WriteAllText(path, "{ broken");
            Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Read(path));
            Assert.Equal("{ broken", File.ReadAllText(path));

            KdpBookSettings.Empty.WithOffset(4, 25).Write(path);
            Assert.Equal(25, KdpBookSettings.Read(path).OffsetFor(4));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
