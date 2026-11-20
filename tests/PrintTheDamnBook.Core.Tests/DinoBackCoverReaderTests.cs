using System.IO.Compression;
using System.Text;
using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Tests;

/// <summary>Slice 23: the back cover on the last page of a <c>.dino</c> file (untrusted input).</summary>
public class DinoBackCoverReaderTests
{
    private const string Manifest = """{"schemaVersion":1,"id":"abc123","title":"The Pigs","language":"en"}""";
    private static readonly byte[] Picture = [0xFF, 0xD8, 0xFF, 0xD9];

    private static string StoryWith(string backCoverPage) => $$"""
        {"Title":"The Pigs","Language":"en","CoverFile":"cover-1.jpg",
         "Pages":[{"Text":"Pip.","ImageFile":"page-1.jpg"},{"Text":"Pop.","ImageFile":"page-2.jpg"}{{backCoverPage}}]}
        """;

    private static string BackPage(string backCover, string imageFile = "back-cover.jpg") =>
        $$""",{"Text":"Story by Aesop","ImageFile":"{{imageFile}}","BackCover":{{backCover}}}""";

    private const string GoodCredits = """{"storyBy":"Aesop","retold":true,"illustration":"Ashkan Sirous","month":"2026-11","picture":"back-cover.jpg"}""";

    private static MemoryStream Package(string story, params string[] pictures)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "package.json", Encoding.UTF8.GetBytes(Manifest));
            Add(zip, "story.json", Encoding.UTF8.GetBytes(story));
            foreach (var name in pictures)
            {
                Add(zip, name, Picture);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        using var entry = zip.CreateEntry(name).Open();
        entry.Write(bytes);
    }

    private static MemoryStream WithPictures(string story) => Package(story, "cover-1.jpg", "page-1.jpg", "page-2.jpg", "back-cover.jpg");

    [Fact]
    public void ABookWithABackCoverTakesThatPageOutOfThePagesAndReadsTheCredits()
    {
        var book = DinoBookReader.Read(WithPictures(StoryWith(BackPage(GoodCredits))));

        Assert.Equal(2, book.Pages.Count);
        Assert.Equal("back-cover.jpg", book.BackCover!.PictureFile);
        var credits = book.BackCover.Credits!;
        Assert.Equal(("Aesop", true, "Ashkan Sirous", 2026, 11), (credits.StoryBy, credits.Retold, credits.Illustration, credits.Year, credits.Month));
    }

    [Fact]
    public void TheStoresPascalCaseKeysReadLikeTheCamelCaseOnes()
    {
        const string pascal = """{"StoryBy":"Aesop","Retold":true,"Illustration":"Ashkan Sirous","Month":"2026-10","Picture":"cover-back.jpg","NamesByLanguage":{"fa":{"Aesop":"ازوپ"}}}""";
        var book = DinoBookReader.Read(Package(StoryWith(BackPage(pascal, "cover-back.jpg")), "cover-1.jpg", "page-1.jpg", "page-2.jpg", "cover-back.jpg"));

        Assert.Equal("cover-back.jpg", book.BackCover!.PictureFile);
        var credits = book.BackCover.Credits!;
        Assert.Equal(("Aesop", true, "Ashkan Sirous", 2026, 10), (credits.StoryBy, credits.Retold, credits.Illustration, credits.Year, credits.Month));
        Assert.Equal("ازوپ", credits.NameIn("Aesop", "fa"));
    }

    [Fact]
    public void ABookWithoutABackCoverKeepsEveryPageAndHasNoBackCover()
    {
        var book = DinoBookReader.Read(WithPictures(StoryWith(string.Empty)));

        Assert.Equal(2, book.Pages.Count);
        Assert.Null(book.BackCover);
    }

    [Theory]
    [InlineData("""{"storyBy":"","month":"2026-11","picture":"back-cover.jpg"}""")]
    [InlineData("""{"storyBy":"Aesop","month":"nope","picture":"back-cover.jpg"}""")]
    [InlineData("""{"storyBy":"Aesop","month":"2026-13","picture":"back-cover.jpg"}""")]
    [InlineData("""{"month":"2026-11","picture":"back-cover.jpg"}""")]
    [InlineData("""{"storyBy":5,"month":"2026-11","picture":"back-cover.jpg"}""")]
    public void ABadCreditFieldDropsOnlyTheCreditsAndThePictureStillPrints(string credits)
    {
        var book = DinoBookReader.Read(WithPictures(StoryWith(BackPage(credits))));

        Assert.Equal("back-cover.jpg", book.BackCover!.PictureFile);
        Assert.Null(book.BackCover.Credits);
        Assert.Equal(2, book.Pages.Count);
    }

    [Fact]
    public void AnOverlongNameIsADroppedField()
    {
        var long_ = new string('x', DinoBackCoverReader.MaxNameChars + 1);

        var book = DinoBookReader.Read(WithPictures(StoryWith(BackPage($$"""{"storyBy":"{{long_}}","month":"2026-11","picture":"back-cover.jpg"}"""))));

        Assert.Null(book.BackCover!.Credits);
    }

    [Fact]
    public void ANamedPictureThatIsNotInThePackageLeavesNoBackCoverButStillRemovesThePage()
    {
        var story = StoryWith(BackPage("""{"storyBy":"Aesop","month":"2026-11","picture":"missing.jpg"}""", "missing.jpg"));

        var book = DinoBookReader.Read(Package(story, "cover-1.jpg", "page-1.jpg", "page-2.jpg"));

        Assert.Null(book.BackCover);
        Assert.Equal(2, book.Pages.Count);
    }

    [Fact]
    public void AnOlderFileNamesThePictureByThePagesImage()
    {
        var book = DinoBookReader.Read(WithPictures(StoryWith(BackPage("""{"storyBy":"Aesop","month":"2026-11"}"""))));

        Assert.Equal("back-cover.jpg", book.BackCover!.PictureFile);
        Assert.NotNull(book.BackCover.Credits);
    }

    [Fact]
    public void ABackCoverObjectOnANonLastPageIsIgnored()
    {
        const string story = """
            {"Title":"T","Language":"en","Pages":[{"Text":"A","ImageFile":"page-1.jpg","BackCover":{"storyBy":"X","month":"2026-11","picture":"back-cover.jpg"}},{"Text":"B","ImageFile":"page-2.jpg"}]}
            """;

        var book = DinoBookReader.Read(WithPictures(story));

        Assert.Null(book.BackCover);
        Assert.Equal(2, book.Pages.Count);
    }

    [Fact]
    public void SpellingsPerLanguageAreReadAndUsed()
    {
        const string credits = """{"storyBy":"Aesop","month":"2026-11","picture":"back-cover.jpg","namesByLanguage":{"fa":{"Aesop":"ازوپ"},"de":"bad"}}""";

        var book = DinoBookReader.Read(WithPictures(StoryWith(BackPage(credits))));

        Assert.Equal("ازوپ", book.BackCover!.Credits!.NameIn("Aesop", "fa-IR"));
        Assert.Equal("Aesop", book.BackCover.Credits.NameIn("Aesop", "de"));
    }

    [Fact]
    public void ABadLanguageEntryDoesNotDropTheValidOnesAfterIt()
    {
        const string credits = """{"storyBy":"Aesop","month":"2026-11","picture":"back-cover.jpg","namesByLanguage":{"de":null,"it":"bad","fa":{"Aesop":"ازوپ"}}}""";

        var book = DinoBookReader.Read(WithPictures(StoryWith(BackPage(credits))));

        Assert.Equal("ازوپ", book.BackCover!.Credits!.NameIn("Aesop", "fa"));
    }

    [Fact]
    public void ALastPageWhosePictureIsUnusableIsCountedAsSkippedNotDroppedSilently()
    {
        const string credits = """{"storyBy":"Aesop","month":"2026-11","picture":"gone.jpg"}""";

        var book = DinoBookReader.Read(WithPictures(StoryWith(BackPage(credits, "gone.jpg"))));

        Assert.Null(book.BackCover);
        Assert.Equal(2, book.Pages.Count);
        Assert.Equal(1, book.SkippedPages);
    }

    [Fact]
    public void ALastPageWithAUsablePictureButAnUnusableBackCoverStaysAStoryPage()
    {
        const string credits = """{"storyBy":"Aesop","month":"2026-11","picture":"gone.jpg"}""";

        var book = DinoBookReader.Read(WithPictures(StoryWith(BackPage(credits, "page-2.jpg"))));

        Assert.Null(book.BackCover);
        Assert.Equal(3, book.Pages.Count);
        Assert.Equal(0, book.SkippedPages);
    }

    [Fact]
    public void PlannedAndExtractedPicturesNameTheBackCoverTheFolderOrderingReads()
    {
        var book = DinoBookReader.Read(WithPictures(StoryWith(BackPage(GoodCredits))));
        var folder = Path.Combine(AppContext.BaseDirectory, "back-cover-" + Guid.NewGuid().ToString("N"));
        try
        {
            var extracted = DinoBookReader.ExtractPictures(WithPictures(StoryWith(BackPage(GoodCredits))), book, folder, TestContext.Current.CancellationToken);

            Assert.Equal("back cover.jpg", DinoBookReader.PlannedPictures(book).BackCoverFile);
            Assert.Equal("back cover.jpg", extracted.BackCoverFile);
            Assert.True(File.Exists(Path.Combine(folder, "back cover.jpg")));
            Assert.Equal(PageKind.BackCover, PageOrdering.FromFolder([Path.Combine(folder, "1.jpg"), Path.Combine(folder, "back cover.jpg")])[^1].Kind);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
