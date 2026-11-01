using System.Text;
using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Tests;

public class DinoStoreCatalogTests
{
    private const string Good = """
        {"schemaVersion":1,"stories":[
          {"id":"a1","title":"Good Enough","language":"en","schemaVersion":1,"version":3,"pageCount":12,"ageMin":4,"ageMax":7,
           "coverUrl":"/api/stories/a1/cover?v=3","downloadUrl":"/api/stories/a1/download?v=3"},
          {"id":"b2","title":"همین هم خوب است","language":"fa","schemaVersion":1,"version":1,"pageCount":1,"ageMin":4,"ageMax":null,
           "coverUrl":null,"downloadUrl":"https://dinoreader.sirous.uk/api/stories/b2/download?v=1"},
          {"id":"c3","title":"Der Ball","language":"de-DE","schemaVersion":1,"version":1,"pageCount":8,"downloadUrl":"/api/stories/c3/download"},
          {"id":"d4","title":"Another","language":"en","schemaVersion":1,"version":2,"pageCount":20,"ageMin":3,"ageMax":3,"downloadUrl":"/api/stories/d4/download"}
        ]}
        """;

    private static string One(string fields) => "{\"stories\":[{" + fields + "}]}";

    [Fact]
    public void GoodCatalogGivesEntriesWithAbsoluteUrls()
    {
        var catalog = DinoStoreCatalog.Parse(Good);

        Assert.Equal(4, catalog.Entries.Count);
        var first = catalog.Entries[0];
        Assert.Equal("a1", first.Id);
        Assert.Equal(3, first.Version);
        Assert.Equal(new Uri("https://dinoreader.sirous.uk/api/stories/a1/cover?v=3"), first.CoverUrl);
        Assert.Equal(new Uri("https://dinoreader.sirous.uk/api/stories/a1/download?v=3"), first.DownloadUrl);
        Assert.Null(catalog.Entries[1].CoverUrl);
    }

    [Fact]
    public void DuplicateIdKeepsTheFirstEntryAndAByteOrderMarkIsIgnored()
    {
        const string json = "﻿{\"stories\":[" +
            "{\"id\":\"x1\",\"title\":\"First\",\"language\":\"en\",\"version\":2}," +
            "{\"id\":\"X1\",\"title\":\"Second\",\"language\":\"en\",\"version\":5}]}";

        var entries = DinoStoreCatalog.Parse(json).Entries;

        Assert.Equal("First", Assert.Single(entries).Title);
    }

    [Fact]
    public void LanguagesAreCountedByPrimaryCode()
    {
        var languages = DinoStoreCatalog.Parse(Good).Languages();

        Assert.Equal(["en", "fa", "de"], languages.Select(l => l.Code));
        Assert.Equal([2, 1, 1], languages.Select(l => l.Count));
        Assert.Equal(["a1", "d4"], DinoStoreCatalog.Parse(Good).InLanguage("en-US").Select(e => e.Id));
        Assert.Equal(4, DinoStoreCatalog.Parse(Good).InLanguage(null).Count);
    }

    [Fact]
    public void CardLinesShowPagesAndAges()
    {
        var entries = DinoStoreCatalog.Parse(Good).Entries;

        Assert.Equal("12 pages · ages 4–7", DinoStoreCatalog.MetaText(entries[0]));
        Assert.Equal("1 page", DinoStoreCatalog.MetaText(entries[1]));
        Assert.Equal("8 pages", DinoStoreCatalog.MetaText(entries[2]));
        Assert.Equal("20 pages · ages 3", DinoStoreCatalog.MetaText(entries[3]));
    }

    [Theory]
    [InlineData("\"title\":\"T\",\"language\":\"en\"")]
    [InlineData("\"id\":\"x\",\"language\":\"en\"")]
    [InlineData("\"id\":\"x\",\"title\":\"T\"")]
    [InlineData("\"id\":\"../x\",\"title\":\"T\",\"language\":\"en\"")]
    [InlineData("\"id\":\"x\",\"title\":\"  \",\"language\":\"en\"")]
    [InlineData("\"id\":\"x\",\"title\":\"T\",\"language\":\"en\",\"version\":0")]
    [InlineData("\"id\":\"x\",\"title\":\"T\",\"language\":\"en\",\"version\":\"two\"")]
    [InlineData("\"id\":5,\"title\":\"T\",\"language\":\"en\"")]
    public void EntryWithMissingOrBadFieldIsDroppedAndTheRestStay(string fields)
    {
        var json = "{\"stories\":[{" + fields + "},{\"id\":\"ok\",\"title\":\"Fine\",\"language\":\"en\",\"downloadUrl\":\"/api/stories/ok/download\"}]}";

        var entries = DinoStoreCatalog.Parse(json).Entries;

        Assert.Equal(["ok"], entries.Select(e => e.Id));
    }

    [Theory]
    [InlineData("https://evil.example/api/stories/x/download")]
    [InlineData("http://dinoreader.sirous.uk/api/stories/x/download")]
    [InlineData("//evil.example/api/stories/x/download")]
    [InlineData("https://dinoreader.sirous.uk.evil.example/api/stories/x/download")]
    [InlineData("https://dinoreader.sirous.uk:8443/api/stories/x/download")]
    [InlineData("https://user@dinoreader.sirous.uk/api/stories/x/download")]
    [InlineData("/other/x/download")]
    [InlineData("/api/stories/../admin")]
    public void EntryWithAForeignDownloadUrlIsDropped(string url)
    {
        var json = One($"\"id\":\"x\",\"title\":\"T\",\"language\":\"en\",\"downloadUrl\":\"{url}\"");

        Assert.Empty(DinoStoreCatalog.Parse(json).Entries);
    }

    [Fact]
    public void ForeignCoverIsDroppedButTheBookStays()
    {
        var json = One("\"id\":\"x\",\"title\":\"T\",\"language\":\"en\",\"coverUrl\":\"https://evil.example/c.jpg\",\"downloadUrl\":\"/api/stories/x/download\"");

        var entry = Assert.Single(DinoStoreCatalog.Parse(json).Entries);

        Assert.Null(entry.CoverUrl);
    }

    [Fact]
    public void MissingDownloadUrlFallsBackToTheStoreDownloadPath()
    {
        var entry = Assert.Single(DinoStoreCatalog.Parse(One("\"id\":\"x\",\"title\":\"T\",\"language\":\"en\"")).Entries);

        Assert.Equal(new Uri("https://dinoreader.sirous.uk/api/stories/x/download"), entry.DownloadUrl);
    }

    [Fact]
    public void BookFromANewerSchemaIsHidden()
    {
        var json = "{\"stories\":[{\"id\":\"n\",\"title\":\"New\",\"language\":\"en\",\"schemaVersion\":2},{\"id\":\"o\",\"title\":\"Old\",\"language\":\"en\",\"schemaVersion\":1}]}";

        Assert.Equal(["o"], DinoStoreCatalog.Parse(json).Entries.Select(e => e.Id));
    }

    [Fact]
    public void BadAgesAndPageCountsAreIgnoredNotFatal()
    {
        var entry = Assert.Single(DinoStoreCatalog.Parse(One("\"id\":\"x\",\"title\":\"T\",\"language\":\"en\",\"ageMin\":9,\"ageMax\":2,\"pageCount\":99999")).Entries);

        Assert.Null(entry.AgeMin);
        Assert.Null(entry.PageCount);
        Assert.Equal(string.Empty, DinoStoreCatalog.MetaText(entry));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"stories\"")]
    [InlineData("not json")]
    [InlineData("")]
    public void BodyThatIsNoListThrowsAUserMessage(string json)
    {
        Assert.Throws<DinoBookException>(() => DinoStoreCatalog.Parse(json));
    }

    [Fact]
    public void ObjectWithoutStoriesIsAnEmptyList()
    {
        Assert.Empty(DinoStoreCatalog.Parse("{\"schemaVersion\":1}").Entries);
    }

    [Fact]
    public void OversizeBodyIsRefused()
    {
        var padding = new StringBuilder("{\"stories\":[],\"pad\":\"").Append('x', DinoStoreCatalog.MaxCatalogBytes).Append("\"}").ToString();

        Assert.Throws<DinoBookException>(() => DinoStoreCatalog.Parse(padding));
    }

    [Fact]
    public void MoreEntriesThanTheCapAreCut()
    {
        var items = Enumerable.Range(0, DinoStoreCatalog.MaxEntries + 20).Select(i => $"{{\"id\":\"s{i}\",\"title\":\"T\",\"language\":\"en\"}}");

        var catalog = DinoStoreCatalog.Parse("{\"stories\":[" + string.Join(',', items) + "]}");

        Assert.Equal(DinoStoreCatalog.MaxEntries, catalog.Entries.Count);
    }

    [Fact]
    public void RightToLeftLanguagesAreNotSupportedYet()
    {
        var entries = DinoStoreCatalog.Parse(Good).Entries;

        Assert.True(DinoStoreCatalog.IsSupported(entries[0]));
        Assert.False(DinoStoreCatalog.IsSupported(entries[1]));
    }

    [Theory]
    [InlineData("https://dinoreader.sirous.uk/api/stories/x/cover", true)]
    [InlineData("https://DINOREADER.sirous.uk/api/stories/x/cover", true)]
    [InlineData("https://dinoreader.sirous.uk/api/stories", false)]
    [InlineData("https://dinoreader.sirous.uk/other", false)]
    [InlineData("http://dinoreader.sirous.uk/api/stories/x/cover", false)]
    public void AllowListChecksSchemeHostAndPath(string url, bool allowed)
    {
        Assert.Equal(allowed, DinoStoreCatalog.IsAllowedUrl(new Uri(url)));
    }

    [Fact]
    public void RealCatalogFetchedOnTheStoreParsesWithAbsoluteUrls()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "live-catalog.json"));

        var catalog = DinoStoreCatalog.Parse(json);

        Assert.Equal(3, catalog.Entries.Count);
        Assert.All(catalog.Entries, e =>
        {
            Assert.Equal(DinoStoreCatalog.Host, e.DownloadUrl.Host);
            Assert.Equal(DinoStoreCatalog.Host, e.CoverUrl!.Host);
            Assert.Equal($"https://dinoreader.sirous.uk/api/stories/{e.Id}/download?v={e.Version}", e.DownloadUrl.ToString());
            Assert.Equal($"https://dinoreader.sirous.uk/api/stories/{e.Id}/cover?v={e.Version}", e.CoverUrl.ToString());
        });
        Assert.Equal(7, catalog.Entries[0].Version);
    }

    [Theory]
    [InlineData("\"downloadUrl\":\"https://evil.example/api/stories/a1/download\"")]
    [InlineData("\"downloadUrl\":\"/elsewhere/a1.dino\"")]
    [InlineData("\"downloadUrl\":\"http://dinoreader.sirous.uk/api/stories/a1/download\"")]
    public void AnEntryWithADisallowedDownloadUrlIsDroppedNotDefaulted(string downloadField)
    {
        var catalog = DinoStoreCatalog.Parse(One("\"id\":\"a1\",\"title\":\"T\",\"language\":\"en\"," + downloadField));

        Assert.Empty(catalog.Entries);
    }

    [Fact]
    public void AnEntryWithNoDownloadUrlGetsTheDefaultOne()
    {
        var catalog = DinoStoreCatalog.Parse(One("\"id\":\"a1\",\"title\":\"T\",\"language\":\"en\""));

        Assert.Equal(new Uri("https://dinoreader.sirous.uk/api/stories/a1/download"), Assert.Single(catalog.Entries).DownloadUrl);
    }

    [Fact]
    public void AnAbsurdVersionDropsTheEntry()
    {
        var catalog = DinoStoreCatalog.Parse(One("\"id\":\"a1\",\"title\":\"T\",\"language\":\"en\",\"version\":2000000000"));

        Assert.Empty(catalog.Entries);
    }
}
