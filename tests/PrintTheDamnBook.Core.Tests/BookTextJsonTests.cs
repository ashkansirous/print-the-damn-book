using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests;

public class BookTextJsonTests
{
    private static BookText Sample() => new()
    {
        TextPages = TextPageBackground.TintedFromCover,
        Pages =
        {
            ["1.jpg"] = new PageTextEntry("Saturday morning.", TextPlacement.OnPicture),
            ["2.jpg"] = new PageTextEntry("Inside, the stadium is enormous.", TextPlacement.OwnPage),
        },
    };

    [Fact]
    public void RoundTrip_KeepsTextPlacementAndTheTextPageSetting()
    {
        var json = BookTextJson.Serialize(Sample());

        var load = BookTextJson.Parse(json);

        Assert.True(load.CanSave);
        Assert.Equal(TextPageBackground.TintedFromCover, load.Book.TextPages);
        Assert.Equal(new PageTextEntry("Inside, the stadium is enormous.", TextPlacement.OwnPage), load.Book.Pages["2.jpg"]);
        Assert.Equal(BookText.CurrentSchemaVersion, load.Book.SchemaVersion);
    }

    [Fact]
    public void Serialize_WritesTheSchemaVersionAndNamedEnums()
    {
        var json = BookTextJson.Serialize(Sample());

        Assert.Contains("\"schemaVersion\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OwnPage", json);
        Assert.Contains("TintedFromCover", json);
    }

    [Fact]
    public void Keys_AreCaseInsensitive()
    {
        var load = BookTextJson.Parse(BookTextJson.Serialize(Sample()));

        Assert.True(load.Book.Pages.ContainsKey("1.JPG"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingOrEmptyFile_ReadsAsNoText_AndCanBeOverwritten(string? json)
    {
        var load = BookTextJson.Parse(json);

        Assert.Empty(load.Book.Pages);
        Assert.Equal(TextPageBackground.White, load.Book.TextPages);
        Assert.True(load.CanSave);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"pages\": {\"1.jpg\": {\"placement\": \"Sideways\"}}}")]
    public void UnparsableFile_ReadsAsNoText_AndIsNotOverwritten(string json)
    {
        var load = BookTextJson.Parse(json);

        Assert.Empty(load.Book.Pages);
        Assert.False(load.CanSave);
    }

    [Fact]
    public void KeyFor_IsThePlainFileNameInTheBookFolder_AndTheFullPathElsewhere()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ptdb-book");
        var inside = Path.Combine(folder, "1.jpg");
        var outside = Path.Combine(Path.GetTempPath(), "ptdb-other", "1.jpg");

        Assert.Equal("1.jpg", BookTextJson.KeyFor(folder + Path.DirectorySeparatorChar, inside));
        Assert.Equal(Path.GetFullPath(outside), BookTextJson.KeyFor(folder, outside));
        Assert.NotEqual(BookTextJson.KeyFor(folder, inside), BookTextJson.KeyFor(folder, outside));
    }

    [Fact]
    public void NewerSchema_ReadsAsNoText_AndIsNotOverwritten()
    {
        var load = BookTextJson.Parse("{\"schemaVersion\": 99, \"pages\": {\"1.jpg\": {\"text\": \"x\"}}}");

        Assert.Empty(load.Book.Pages);
        Assert.False(load.CanSave);
    }

    [Fact]
    public void EntriesWithoutWords_AreDropped_AndBadSettingsRepaired()
    {
        var load = BookTextJson.Parse("{\"textPages\": 42, \"pages\": {\"1.jpg\": {\"text\": \"  \"}, \"2.jpg\": null, \"3.jpg\": {\"text\": \"Hi\", \"placement\": 9}}}");

        Assert.Equal(TextPageBackground.White, load.Book.TextPages);
        var only = Assert.Single(load.Book.Pages);
        Assert.Equal("3.jpg", only.Key);
        Assert.Equal(TextPlacement.OnPicture, only.Value.Placement);
    }

    [Fact]
    public void Load_OfAMissingFile_IsNoText()
    {
        var load = BookTextJson.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), BookText.FileName));

        Assert.Empty(load.Book.Pages);
        Assert.True(load.CanSave);
    }

    [Fact]
    public void TrySave_ThenLoad_RoundTripsThroughTheFile()
    {
        var folder = Directory.CreateTempSubdirectory("ptdb-booktext").FullName;
        try
        {
            var path = Path.Combine(folder, BookText.FileName);

            Assert.True(BookTextJson.TrySave(path, Sample()));
            var load = BookTextJson.Load(path);

            Assert.Equal(2, load.Book.Pages.Count);
            Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void TrySave_IntoAMissingFolder_ReturnsFalse()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), BookText.FileName);

        Assert.False(BookTextJson.TrySave(path, Sample()));
    }
}
