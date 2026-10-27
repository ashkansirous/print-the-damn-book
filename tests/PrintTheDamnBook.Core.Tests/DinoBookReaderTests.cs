using System.IO.Compression;
using System.Text;
using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Tests;

public class DinoBookReaderTests
{
    private const string Manifest = """{"schemaVersion":1,"id":"abc123","title":"The Pigs","language":"en"}""";

    private const string Story = """
        {"Title":"The Pigs","Language":"en","CoverFile":"cover-1.jpg",
         "Pages":[{"Text":"Pip was in a hurry. \"Now I can play,\" said Pip.","ImageFile":"page-1.jpg","Lines":null},
                  {"Text":"Pop wanted to play too.","ImageFile":"page-2.jpg"}],
         "Cast":[{"Name":"Pip"}],"Recordings":{},
         "Translations":{"de":{"Pip was in a hurry.":"Pip hatte es eilig.","Now I can play,":"Jetzt kann ich spielen,","said Pip.":"sagte Pip.","Pop wanted to play too.":"Pop wollte auch spielen."},
                         "fa":{"Pop wanted to play too.":"x"},"bad":"nope"}}
        """;

    private static readonly byte[] Picture = [0xFF, 0xD8, 0xFF, 0xD9];

    private static MemoryStream Zip(params (string Name, string? Text, byte[]? Bytes)[] files)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text, bytes) in files)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(bytes ?? Encoding.UTF8.GetBytes(text ?? string.Empty));
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream GoodBook(string story = Story, string manifest = Manifest) => Zip(
        ("package.json", manifest, null),
        ("story.json", story, null),
        ("cover-1.jpg", null, Picture),
        ("page-1.jpg", null, Picture),
        ("page-2.jpg", null, Picture));

    [Fact]
    public void Read_GivesTitleLanguageCoverAndPagesWithWords()
    {
        var book = DinoBookReader.Read(GoodBook());

        Assert.Equal("The Pigs", book.Title);
        Assert.Equal("en", book.Language);
        Assert.Equal("cover-1.jpg", book.CoverFile);
        Assert.Equal(2, book.Pages.Count);
        Assert.Equal("Pop wanted to play too.", book.Pages[1].Text);
        Assert.Equal("page-2.jpg", book.Pages[1].ImageFile);
    }

    [Fact]
    public void Read_RefusesAPackageFromANewerSchema()
    {
        var ex = Assert.Throws<DinoBookException>(() => DinoBookReader.Read(GoodBook(manifest: """{"schemaVersion":2,"id":"x"}""")));

        Assert.Contains("newer version", ex.Message);
    }

    [Fact]
    public void Read_RefusesAPackageWithoutStoryJson()
    {
        using var zip = Zip(("package.json", Manifest, null), ("page-1.jpg", null, Picture));

        var ex = Assert.Throws<DinoBookException>(() => DinoBookReader.Read(zip));

        Assert.Contains("story.json", ex.Message);
    }

    [Fact]
    public void Read_RefusesAPackageWithoutPackageJson()
    {
        using var zip = Zip(("story.json", Story, null));

        Assert.Throws<DinoBookException>(() => DinoBookReader.Read(zip));
    }

    [Fact]
    public void Read_LeavesOutAPageWhoseImageIsMissingAndCountsIt()
    {
        using var zip = Zip(("package.json", Manifest, null), ("story.json", Story, null), ("page-2.jpg", null, Picture));

        var book = DinoBookReader.Read(zip);

        Assert.Single(book.Pages);
        Assert.Equal("page-2.jpg", book.Pages[0].ImageFile);
        Assert.Equal(1, book.SkippedPages);
        Assert.Null(book.CoverFile);
    }

    [Fact]
    public void Read_RefusesABookWithNoUsablePage()
    {
        using var zip = Zip(("package.json", Manifest, null), ("story.json", Story, null));

        Assert.Throws<DinoBookException>(() => DinoBookReader.Read(zip));
    }

    [Fact]
    public void Read_RefusesAnEntryThatCouldLeaveTheFolder()
    {
        using var zip = Zip(("package.json", Manifest, null), ("story.json", Story, null), ("page-1.jpg", null, Picture), ("../evil.jpg", null, Picture));

        var ex = Assert.Throws<DinoBookException>(() => DinoBookReader.Read(zip));

        Assert.Contains("not allowed", ex.Message);
    }

    [Theory]
    [InlineData("sub/page-1.jpg")]
    [InlineData("C:evil.jpg")]
    [InlineData("NUL.jpg")]
    public void Read_RefusesOtherUnsafeNames(string name)
    {
        using var zip = Zip(("package.json", Manifest, null), ("story.json", Story, null), (name, null, Picture));

        Assert.Throws<DinoBookException>(() => DinoBookReader.Read(zip));
    }

    [Fact]
    public void Read_RefusesAnOversizedStoryFile()
    {
        using var zip = Zip(("package.json", Manifest, null), ("story.json", new string(' ', (int)DinoBookReader.MaxStoryBytes + 1), null));

        Assert.Throws<DinoBookException>(() => DinoBookReader.Read(zip));
    }

    [Fact]
    public void Read_RefusesBrokenJsonAndANonZip()
    {
        using var broken = Zip(("package.json", Manifest, null), ("story.json", "{not json", null));
        using var notZip = new MemoryStream(Encoding.UTF8.GetBytes("this is not a zip"));

        Assert.Throws<DinoBookException>(() => DinoBookReader.Read(broken));
        Assert.Throws<DinoBookException>(() => DinoBookReader.Read(notZip));
    }

    [Fact]
    public void Read_ToleratesAByteOrderMarkAndMacMetadata()
    {
        using var zip = Zip(
            ("package.json", "﻿" + Manifest, null),
            ("story.json", "﻿" + Story, null),
            ("page-1.jpg", null, Picture),
            ("__MACOSX/page-1.jpg", null, Picture));

        Assert.Single(DinoBookReader.Read(zip).Pages);
    }

    [Fact]
    public void Languages_ListsTheOwnLanguageFirstThenTranslationsWithRightToLeftFlagged()
    {
        var languages = DinoBookReader.Read(GoodBook()).Languages();

        Assert.Equal("en", languages[0].Code);
        Assert.True(languages[0].IsOwn);
        Assert.Contains(languages, l => l.Code == "de" && !l.IsRightToLeft);
        Assert.Contains(languages, l => l.Code == "fa" && l.IsRightToLeft);
        Assert.DoesNotContain(languages, l => l.Code == "bad");
    }

    [Fact]
    public void In_TranslatesEachSentenceAndKeepsTheQuotes()
    {
        var book = DinoBookReader.Read(GoodBook());

        var german = book.In(book.Pages[0].Text, "de");

        Assert.Equal("Pip hatte es eilig. \"Jetzt kann ich spielen,\" sagte Pip.", german);
    }

    [Fact]
    public void Translate_KeepsTheWhitespaceAroundAMatchedKey()
    {
        var translator = new DinoTranslator(new Dictionary<string, string> { ["Hello. "] = "Hallo.", ["World\n"] = "Welt" });

        Assert.Equal("Hallo. Welt\n", translator.Translate("Hello. World\n"));
    }

    [Fact]
    public void In_KeepsTheOwnLanguageAndLeavesUnknownSentencesAsWritten()
    {
        var book = DinoBookReader.Read(GoodBook());

        Assert.Equal(book.Pages[0].Text, book.In(book.Pages[0].Text, "en"));
        Assert.Equal(book.Pages[0].Text, book.In(book.Pages[0].Text, null));
        Assert.Equal("Something else.", book.In("Something else.", "de"));
        Assert.Equal("Pop wanted to play too.", book.In("Pop wanted to play too.", "xx"));
    }

    [Fact]
    public void ExtractPictures_WritesCoverAndNumberedPagesInOrderOfTheBook()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ptdb-dino-" + Guid.NewGuid().ToString("N"));
        try
        {
            var book = DinoBookReader.Read(GoodBook());

            var pictures = DinoBookReader.ExtractPictures(GoodBook(), book, folder, TestContext.Current.CancellationToken);

            Assert.Equal("cover.jpg", pictures.CoverFile);
            Assert.Equal(["1.jpg", "2.jpg"], pictures.PageFiles);
            Assert.Equal(Picture, File.ReadAllBytes(Path.Combine(folder, "2.jpg")));
            var order = PageOrdering.FromFolder(Directory.GetFiles(folder));
            Assert.Equal([PageKind.Cover, PageKind.Numbered, PageKind.Numbered], order.Select(p => p.Kind));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "ptdb-dino-" + Guid.NewGuid().ToString("N"));

    private static string StoryOf(int pages, string image = "page-1.jpg", string extra = "")
    {
        var list = string.Join(",", Enumerable.Range(0, pages).Select(_ => $"{{\"Text\":\"t\",\"ImageFile\":\"{image}\"}}"));
        return $"{{\"Title\":\"T\",\"Language\":\"en\",{extra}\"Pages\":[{list}]}}";
    }

    [Theory]
    [InlineData("..\evil.jpg")]
    [InlineData("a/../../evil.jpg")]
    [InlineData("/abs.jpg")]
    public void Read_RefusesZipSlipNames(string name)
    {
        using var zip = Zip(("package.json", Manifest, null), ("story.json", Story, null), (name, null, Picture));

        Assert.Throws<DinoBookException>(() => DinoBookReader.Read(zip));
    }

    [Fact]
    public void Read_RefusesAnOversizedPicture()
    {
        using var zip = Zip(("package.json", Manifest, null), ("story.json", Story, null), ("page-1.jpg", null, new byte[DinoBookReader.MaxPictureBytes + 1]));

        Assert.Throws<DinoBookException>(() => DinoBookReader.Read(zip));
    }

    [Fact]
    public void Read_RefusesTooManyPages()
    {
        using var zip = Zip(("package.json", Manifest, null), ("story.json", StoryOf(DinoBookReader.MaxPages + 1), null), ("page-1.jpg", null, Picture));

        var ex = Assert.Throws<DinoBookException>(() => DinoBookReader.Read(zip));

        Assert.Contains("too many pages", ex.Message);
    }

    [Fact]
    public void ExtractPictures_StopsWhenRepeatedReferencesWouldWriteMoreThanTheTotalCap()
    {
        var folder = TempFolder();
        try
        {
            var big = new byte[(int)DinoBookReader.MaxPictureBytes - 1];
            using var zip = Zip(("package.json", Manifest, null), ("story.json", StoryOf(DinoBookReader.MaxPages), null), ("page-1.jpg", null, big));
            var book = DinoBookReader.Read(zip);
            zip.Position = 0;

            Assert.Throws<DinoBookException>(() => DinoBookReader.ExtractPictures(zip, book, folder, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void ExtractPictures_CopiesARepeatedPictureInsteadOfInflatingItAgain()
    {
        var folder = TempFolder();
        try
        {
            using var zip = Zip(("package.json", Manifest, null), ("story.json", StoryOf(3), null), ("page-1.jpg", null, Picture));
            var book = DinoBookReader.Read(zip);
            zip.Position = 0;

            var pictures = DinoBookReader.ExtractPictures(zip, book, folder, TestContext.Current.CancellationToken);

            Assert.Equal(["1.jpg", "2.jpg", "3.jpg"], pictures.PageFiles);
            Assert.Equal(Picture, File.ReadAllBytes(Path.Combine(folder, "3.jpg")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ExtractPictures_ReplacesAStaleCachedFileOfTheSameLength()
    {
        var folder = TempFolder();
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "2.jpg"), [0x00, 0x01, 0x02, 0x03]);
            var book = DinoBookReader.Read(GoodBook());

            DinoBookReader.ExtractPictures(GoodBook(), book, folder, TestContext.Current.CancellationToken);

            Assert.Equal(Picture, File.ReadAllBytes(Path.Combine(folder, "2.jpg")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Read_UsesTheChosenPagesPictureAsTheCover()
    {
        var story = StoryOf(1, extra: "\"CoverStyle\":\"Picture\",\"CoverPageIndex\":0,");
        using var zip = Zip(("package.json", Manifest, null), ("story.json", story, null), ("page-1.jpg", null, Picture));

        var book = DinoBookReader.Read(zip);

        Assert.Equal("page-1.jpg", book.CoverFile);
    }

    [Fact]
    public void Read_RefusesTooManyTranslationEntries()
    {
        var entries = string.Join(",", Enumerable.Range(0, DinoBookReader.MaxTranslationEntries + 1).Select(i => $"\"k{i}\":\"v\""));
        var story = StoryOf(1, extra: $"\"Translations\":{{\"de\":{{{entries}}}}},");
        using var zip = Zip(("package.json", Manifest, null), ("story.json", story, null), ("page-1.jpg", null, Picture));

        Assert.Throws<DinoBookException>(() => DinoBookReader.Read(zip));
    }

    [Fact]
    public void StartLanguage_SkipsARightToLeftOwnLanguageAndIsNullWhenAllAre()
    {
        var persian = new DinoBook("x", "t", "fa", null, [], new Dictionary<string, IReadOnlyDictionary<string, string>> { ["de"] = new Dictionary<string, string> { ["a"] = "b" } });
        var onlyRtl = new DinoBook("x", "t", "fa", null, [], new Dictionary<string, IReadOnlyDictionary<string, string>>());

        Assert.Equal("de", persian.StartLanguage()?.Code);
        Assert.Null(onlyRtl.StartLanguage());
    }
}
