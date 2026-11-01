using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Tests;

public class DinoCacheLayoutTests
{
    private const string Guid32 = "0123456789abcdef0123456789abcdef";

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ptdb-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    [Fact]
    public void Manifest_round_trips()
    {
        var lines = DinoCacheLayout.FormatManifest([("a.png", 10), ("b.png", 0)]);

        var parsed = DinoCacheLayout.ParseManifest(lines);

        Assert.Equal(new Dictionary<string, long> { ["a.png"] = 10, ["b.png"] = 0 }, parsed);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("x\ta.png")]
    [InlineData("-5\ta.png")]
    public void Malformed_manifest_line_is_rejected(string line)
    {
        Assert.Null(DinoCacheLayout.ParseManifest([line]));
    }

    [Fact]
    public void Repeated_name_is_rejected()
    {
        Assert.Null(DinoCacheLayout.ParseManifest(["1\ta.png", "2\tA.png"]));
    }

    [Fact]
    public void Matches_needs_the_same_names_and_sizes()
    {
        var listed = new Dictionary<string, long> { ["a.png"] = 10 };

        Assert.True(DinoCacheLayout.Matches(listed, ["a.png"], _ => 10));
        Assert.False(DinoCacheLayout.Matches(listed, ["a.png"], _ => 9));
        Assert.False(DinoCacheLayout.Matches(listed, ["a.png"], _ => null));
        Assert.False(DinoCacheLayout.Matches(listed, ["a.png", "b.png"], _ => 10));
        Assert.False(DinoCacheLayout.Matches(null, ["a.png"], _ => 10));
    }

    [Fact]
    public void Verify_follows_the_files_on_disk()
    {
        var folder = NewFolder();
        try
        {
            File.WriteAllText(Path.Combine(folder, "a.png"), "12345");
            Assert.False(DinoCacheLayout.Verify(folder, ["a.png"]));

            DinoCacheLayout.WriteManifest(folder, ["a.png"]);
            Assert.True(DinoCacheLayout.Verify(folder, ["a.png"]));

            File.WriteAllText(Path.Combine(folder, "a.png"), "123");
            Assert.False(DinoCacheLayout.Verify(folder, ["a.png"]));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("book-0123456789abcdef.tmp-" + Guid32, true)]
    [InlineData("book-0123456789abcdef.OLD-" + Guid32, true)]
    [InlineData("book-0123456789abcdef.alt-" + Guid32, true)]
    [InlineData("book-0123456789abcdef", false)]
    [InlineData("book.tmp-short", false)]
    public void Leftovers_are_recognised(string name, bool expected)
    {
        Assert.Equal(expected, DinoCacheLayout.IsLeftover(name));
    }

    [Theory]
    [InlineData("book-aaaaaaaaaaaaaaaa", true)]
    [InlineData("book-0123456789abcdef", false)]
    [InlineData("other-aaaaaaaaaaaaaaaa", false)]
    [InlineData("book-aaaaaaaaaaaaaaaa.tmp-" + Guid32, false)]
    public void Only_older_versions_of_the_same_book_match(string name, bool expected)
    {
        Assert.Equal(expected, DinoCacheLayout.IsOlderVersion(name, "book", "book-0123456789abcdef"));
    }
}
