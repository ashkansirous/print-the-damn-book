using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PrintTheDamnBook.App.Printing;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.App.Settings;
using PrintTheDamnBook.App.ViewModels;
using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.App.Tests;

/// <summary>Slice 25: switching a Dino book's language translates every page the user did not change, and names the pages it left alone.</summary>
public sealed class DinoLanguageSwitchTests : IDisposable
{
    private const string One = "1.jpg";
    private const string Two = "2.jpg";
    private const string Fa = "fa";
    private const string En = "en";
    private const string TitleEn = "Love from Seagull Island";
    private const string P1A = "One sunny morning, Pancake Polly fished a bottle out of the sea.";
    private const string P1B = "\"Seagull! This letter is for you!\" said Pancake Polly.";
    private const string DefaultTitleFa = "با عشق، از جزیره مرغ دریایی";
    private const string P2 = "Seagull held the picture in her wings.";

    private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "app-tests", "lang-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public async Task ASavedBookWhosePagesLostTheirBlankLinesStillSwitchesEveryPage()
    {
        var savedFile = Path.Combine(AppContext.BaseDirectory, "TestData", "seagull-saved-book.json");
        var saved = BookTextJson.Load(savedFile).Book.Pages;
        Assert.DoesNotContain("\n\n", saved[One].Text);
        var opened = Opened("saved", saved[One].Text.Replace("\n", "\n\n"), saved[Two].Text.Replace("\n", "\n\n"), saved["cover.jpg"].Text);
        File.Copy(savedFile, BookTextStore.PathIn(opened.Folder));
        var model = NewMain();
        await ShowAsync(model, opened);
        Assert.Equal(Fa, model.SelectedDinoLanguage);

        Assert.StartsWith("ترجمه", TextOf(model, One));
        Assert.StartsWith("ترجمه", TextOf(model, Two));
        Assert.DoesNotContain("Seagull", TextOf(model, One));
        Assert.Null(model.BookNotice);
        Assert.Null(model.KeptTextNotice);
    }

    [Fact]
    public async Task SwitchingAFarsiSavedBookToEnglishNamesNoPageAndTranslatesAll()
    {
        var model = await OpenSavedFarsi("saved-en");

        await SwitchAsync(model, En);

        Assert.StartsWith(P2, TextOf(model, Two));
        Assert.DoesNotContain("ترجمه", TextOf(model, Two));
        Assert.DoesNotContain("ترجمه", TextOf(model, One));
        Assert.Null(model.KeptTextNotice);
        Assert.Null(model.BookNotice);
    }

    [Fact]
    public async Task APageAlreadyInTheTargetLanguageIsNotNamedAsKept()
    {
        var opened = Opened("already");
        var book = new BookText { Pages = new Dictionary<string, PageTextEntry> { [One] = new(opened.Book.In(SourceOne, Fa), TextPlacement.OnPicture, TextBand.Bottom) } };
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(book));
        var model = NewMain();
        await ShowAsync(model, opened);

        await SwitchAsync(model, Fa);

        Assert.Equal(opened.Book.In(SourceOne, Fa), TextOf(model, One));
        Assert.Null(model.KeptTextNotice);
    }

    [Fact]
    public async Task AKeptPageIsNamedByItsPrintedNumberAndStaysNamedAfterLaterRefreshes()
    {
        var opened = Opened("numbers");
        var book = new BookText { Pages = new Dictionary<string, PageTextEntry> { [Two] = new("Mine.", TextPlacement.OnPicture, TextBand.Bottom) } };
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(book));
        var model = NewMain();
        await ShowAsync(model, opened);

        await SwitchAsync(model, Fa);

        var printed = model.Sheets.SelectMany(s => new[] { s.Front.Left, s.Front.Right, s.Back.Left, s.Back.Right }).First(t => t.Label == Path.GetFileNameWithoutExtension(Two)).PrintedNumber;
        Assert.True(printed > 0);
        Assert.Equal($"Page {printed} kept your own text.", model.KeptTextNotice);
        model.Status = "later";
        Assert.Equal($"Page {printed} kept your own text.", model.KeptTextNotice);
    }

    [Fact]
    public async Task AnEditedPageOnOpenKeepsItsWordsWithoutANotice()
    {
        var opened = Opened("open-edited");
        var book = new BookText { Language = Fa, Pages = new Dictionary<string, PageTextEntry> { [One] = new("My own words.", TextPlacement.OnPicture, TextBand.Bottom), [Two] = new(P2, TextPlacement.OnPicture, TextBand.Bottom) } };
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(book));
        var model = NewMain();

        await ShowAsync(model, opened);

        Assert.Equal("My own words.", TextOf(model, One));
        Assert.Equal(opened.Book.In(P2, Fa), TextOf(model, Two));
        Assert.Null(model.KeptTextNotice);
    }

    [Fact]
    public async Task OpeningASavedFarsiBookReconcilesPagesOffTheUiThreadAndEndsNotBusy()
    {
        var opened = Opened("open-async");
        var book = new BookText { Language = Fa, Pages = new Dictionary<string, PageTextEntry> { [One] = new(SourceOne, TextPlacement.OnPicture, TextBand.Bottom) } };
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(book));
        var model = NewMain();

        model.ShowOpened(opened);
        await model.LanguageSwitch;

        Assert.Equal(opened.Book.In(SourceOne, Fa), TextOf(model, One));
        Assert.False(model.IsTranslating);
        Assert.Null(model.KeptTextNotice);
    }

    [Fact]
    public async Task ASavedLanguageTheBookLacksSelectsTheOriginalLanguageAndKeepsItsWords()
    {
        var opened = Opened("open-missing");
        var book = new BookText { Language = "de", Pages = new Dictionary<string, PageTextEntry> { [One] = new(SourceOne, TextPlacement.OnPicture, TextBand.Bottom) } };
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(book));
        var model = NewMain();

        await ShowAsync(model, opened);

        Assert.Equal(En, model.SelectedDinoLanguage);
        Assert.Equal(SourceOne, TextOf(model, One));
    }

    [Fact]
    public async Task ASwitchStartedDuringTheOpenTimeReconcileEndsWithTheSwitchedLanguageOnEveryPage()
    {
        var opened = Opened("during-reconcile");
        var book = new BookText { Language = Fa, Pages = new Dictionary<string, PageTextEntry> { [One] = new(SourceOne, TextPlacement.OnPicture, TextBand.Bottom), [Two] = new(P2, TextPlacement.OnPicture, TextBand.Bottom) } };
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(book));
        var model = NewMain();
        using var gate = new ManualResetEventSlim(false);
        var calls = 0;
        model.TranslateProbe = _ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                gate.Wait(TimeSpan.FromSeconds(10));
            }
        };
        model.ShowOpened(opened);
        var reconcile = model.LanguageSwitch;

        model.SelectedDinoLanguage = En;
        var switched = model.LanguageSwitch;
        gate.Set();
        await Task.WhenAll(reconcile, switched);

        Assert.Equal(En, model.SelectedDinoLanguage);
        Assert.Equal(SourceOne, TextOf(model, One));
        Assert.Equal(P2, TextOf(model, Two));
        Assert.False(model.IsTranslating);
    }

    [Fact]
    public async Task ClosingTheBookDuringARunLeavesTheNextBookUntouched()
    {
        var first = Opened("close-a");
        File.WriteAllText(BookTextStore.PathIn(first.Folder), BookTextJson.Serialize(new BookText { Language = Fa }));
        var next = Opened("close-b");
        var model = NewMain();
        using var gate = new ManualResetEventSlim(false);
        var calls = 0;
        model.TranslateProbe = _ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                gate.Wait(TimeSpan.FromSeconds(10));
            }
        };
        model.ShowOpened(first);
        var stale = model.LanguageSwitch;

        model.ShowOpened(next);
        await model.LanguageSwitch;
        gate.Set();
        await stale;

        Assert.Equal(En, model.SelectedDinoLanguage);
        Assert.Equal(SourceOne, TextOf(model, One));
        Assert.DoesNotContain("Could not change", model.Status);
        Assert.Null(model.KeptTextNotice);
        Assert.False(model.IsTranslating);
    }

    [Fact]
    public async Task AFailedOpenTimeReconcileSetsNoStatusAndLeavesThePagesAsSaved()
    {
        var opened = Opened("reconcile-fails");
        var book = new BookText { Language = Fa, Pages = new Dictionary<string, PageTextEntry> { [One] = new(SourceOne, TextPlacement.OnPicture, TextBand.Bottom) } };
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(book));
        var model = NewMain();
        model.TranslateProbe = _ => throw new InvalidOperationException("boom");

        await ShowAsync(model, opened);

        Assert.DoesNotContain("Could not change", model.Status);
        Assert.Equal(SourceOne, TextOf(model, One));
        Assert.False(model.IsTranslating);
    }

    private async Task<MainViewModel> OpenSavedFarsi(string name)
    {
        var savedFile = Path.Combine(AppContext.BaseDirectory, "TestData", "seagull-saved-book.json");
        var saved = BookTextJson.Load(savedFile).Book.Pages;
        var opened = Opened(name, saved[One].Text.Replace("\n", "\n\n"), saved[Two].Text.Replace("\n", "\n\n"), saved["cover.jpg"].Text);
        File.Copy(savedFile, BookTextStore.PathIn(opened.Folder));
        var model = NewMain();
        await ShowAsync(model, opened);
        return model;
    }

    [Fact]
    public async Task APageTheUserChangedKeepsItsWordsAndIsNamed()
    {
        var opened = Opened("edited");
        var book = new BookText { Pages = new Dictionary<string, PageTextEntry> { [One] = new("My own words.", TextPlacement.OnPicture, TextBand.Bottom), [Two] = new(P2, TextPlacement.OnPicture, TextBand.Bottom) } };
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(book));
        var model = NewMain();
        await ShowAsync(model, opened);

        await SwitchAsync(model, Fa);

        Assert.Equal("My own words.", TextOf(model, One));
        Assert.Equal(opened.Book.In(P2, Fa), TextOf(model, Two));
        Assert.Equal("Page 1 kept your own text.", model.KeptTextNotice);
    }

    [Fact]
    public async Task SwitchingTheLanguageAndBackKeepsEachParagraphBreakInTheModel()
    {
        var opened = Opened("roundtrip");
        var model = NewMain();
        await ShowAsync(model, opened);
        Assert.Equal(SourceOne, TextOf(model, One));

        await SwitchAsync(model, Fa);
        await SwitchAsync(model, En);

        Assert.Equal(SourceOne, TextOf(model, One));
    }

    [Fact]
    public async Task APageWithOnlyBlankLinesChangedCountsAsNotEdited()
    {
        var opened = Opened("blank");
        var book = new BookText { Pages = new Dictionary<string, PageTextEntry> { [One] = new(SourceOne.Replace("\n\n", "\r\n \r\n"), TextPlacement.OnPicture, TextBand.Bottom) } };
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(book));
        var model = NewMain();
        await ShowAsync(model, opened);

        await SwitchAsync(model, Fa);

        Assert.Equal(opened.Book.In(SourceOne, Fa), TextOf(model, One));
        Assert.Null(model.BookNotice);
    }

    // ---- Helpers --------------------------------------------------------------------------------------

    private static string SourceOne => P1A + "\n\n" + P1B;

    /// <summary>Opens the book and waits for the off-thread reconciling of its pages to the saved language.</summary>
    private static async Task ShowAsync(MainViewModel model, OpenedDino opened)
    {
        model.ShowOpened(opened);
        await model.LanguageSwitch;
    }

    private static async Task SwitchAsync(MainViewModel model, string code)
    {
        model.SelectedDinoLanguage = code;
        await model.LanguageSwitch;
    }

    private static string TextOf(MainViewModel model, string file)
    {
        return model.BookPages.Single(p => Path.GetFileName(p.Path) == file).Story?.Text ?? string.Empty;
    }

    private MainViewModel NewMain()
    {
        var print = new PrintViewModel(new PrinterService(), new SilentPrompts(), new SettingsStore(Path.Combine(_dir, "settings.json")));
        return new MainViewModel(() => null, print, () => null);
    }

    private OpenedDino Opened(string name, string? one = null, string? two = null, string titleFa = DefaultTitleFa)
    {
        var folder = Path.Combine(_dir, name);
        var package = Package(one ?? SourceOne, two ?? P2, titleFa);
        var book = DinoBookReader.Read(new MemoryStream(package));
        var pictures = DinoBookReader.ExtractPictures(new MemoryStream(package), book, folder);
        File.WriteAllBytes(Path.Combine(folder, DinoBookCache.BackCoverFile), [0x89]);
        var files = new[] { pictures.CoverFile! }.Concat(pictures.PageFiles).Append(DinoBookCache.BackCoverFile).Select(f => Path.Combine(folder, f)).ToList();
        return new OpenedDino(Path.Combine(_dir, name + ".dino"), folder, book, files, pictures);
    }

    /// <summary>A Farsi table with one entry per paragraph (the pieces Dino Reader translates), so each page's words have a Farsi form.</summary>
    private static Dictionary<string, string> FarsiTable(string titleFa, params string[] pages)
    {
        var table = new Dictionary<string, string> { [TitleEn] = titleFa };
        foreach (var paragraph in pages.SelectMany(p => p.Split("\n\n")))
        {
            table[paragraph] = "ترجمه " + table.Count;
        }

        return table;
    }

    private static byte[] Package(string one, string two, string titleFa)
    {
        var story = new
        {
            Title = TitleEn,
            Language = En,
            CoverFile = "c.jpg",
            Pages = new[] { new { Text = one, ImageFile = "p1.jpg" }, new { Text = two, ImageFile = "p2.jpg" } },
            Translations = new Dictionary<string, Dictionary<string, string>> { [Fa] = FarsiTable(titleFa, one, two) },
        };
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = """{"schemaVersion":1,"id":"seagull","title":"Love from Seagull Island","language":"en"}""";
            foreach (var (name, bytes) in new[] { ("package.json", Encoding.UTF8.GetBytes(manifest)), ("story.json", JsonSerializer.SerializeToUtf8Bytes(story)), ("c.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }), ("p1.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }), ("p2.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }) })
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(bytes);
            }
        }

        return stream.ToArray();
    }

    private sealed class SilentPrompts : IPrintPrompts
    {
        public void ShowError(string message)
        {
        }

        public bool ConfirmContinue(string message) => false;

        public bool ConfirmPrintBacks(string message) => false;

        public bool AskBackUpsideDown() => false;

        public bool ConfirmRetryBacks(string message) => false;
    }
}
