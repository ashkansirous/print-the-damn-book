using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PrintTheDamnBook.App.Printing;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.App.Settings;
using PrintTheDamnBook.App.ViewModels;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.App.Tests;

/// <summary>Slice 23: no empty lines in the layout, words that do not fit move to their own page, and a Dino book's back cover prints with its credits.</summary>
public sealed class StoryFitAndBackCoverTests : IDisposable
{
    private const string Long = "One sunny morning, Pancake Polly fished a bottle out of the sea and held it up to the light so she could read the tiny letter inside. " +
        "\"Seagull! This letter is for you!\" said Pancake Polly, and she ran all the way along the sandy shore, past the lighthouse and the old wooden boats, " +
        "to the very end of the island where the seagull lived in a nest made of rope, shells and bits of coloured glass, and she waved the bottle over her head " +
        "while the waves splashed her feet and the wind blew her hat into the water and the whole beach laughed.";

    private const string ShortWithBlanks = "Pip ran.\n\nPip hid.\n\nPip sat.\n\nPip sang.\n\nPip slept.";
    private const string Fa = "fa";

    private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "app-tests", "fit-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    // ---- Empty lines ----------------------------------------------------------------------------------

    [Fact]
    public void AnEmptyLineBetweenLinesNeverCostsRoomInTheBand()
    {
        var visible = new RectD(0, 0, 148, 210);

        var withBlanks = StoryPainter.FitBand(ShortWithBlanks, PaperKind.A4, visible);
        var without = StoryPainter.FitBand(StoryLines.Clean(ShortWithBlanks), PaperKind.A4, visible);
        var tooMany = StoryPainter.FitBand(string.Join('\n', Enumerable.Repeat("Pip ran.", 9)), PaperKind.A4, visible);

        Assert.True(withBlanks.Fits);
        Assert.Equal(without.Pt, withBlanks.Pt);
        Assert.False(tooMany.Fits);
    }

    [Fact]
    public void AnEmptyLineBetweenLinesNeverCostsRoomOnATextPage()
    {
        var area = StoryTextLayout.TextPage(Paper.LandscapeSheetMm(PaperKind.A4), false, 5, PageNumberPosition.Off);
        var lines = string.Join("\n\n", Enumerable.Repeat("Pip ran across the whole wide beach.", 9));

        var withBlanks = StoryPainter.FitPage(lines, PaperKind.A4, area.Text);
        var without = StoryPainter.FitPage(StoryLines.Clean(lines), PaperKind.A4, area.Text);

        Assert.Equal(without, withBlanks);
    }

    // ---- Overflow moves by itself ---------------------------------------------------------------------

    [Fact]
    public Task WordsThatDoNotFitTheBandMoveToTheirOwnPageAndAreNamed() => OnPump(async () =>
    {
        var model = NewMain();

        await ShowAsync(model, Opened("moves", Long, ShortWithBlanks));

        var long1 = PageOf(model, "1.jpg");
        Assert.Equal(TextPlacement.OwnPage, long1.Story!.Placement);
        Assert.False(long1.Story.ChosenByUser);
        Assert.Equal(TextPlacement.OnPicture, PageOf(model, "2.jpg").Story!.Placement);
        Assert.Equal("Moved to its own page so the words fit: page 2.", model.MovedTextNotice);
        Assert.Contains(model.Sheets.SelectMany(Tiles), t => t.IsText);
    });

    [Fact]
    public Task APageTheUserKeptOnThePictureIsNotMovedAndKeepsTheTooLongWarning() => OnPump(async () =>
    {
        var opened = Opened("kept", Long, ShortWithBlanks);
        var book = new BookText { Pages = new Dictionary<string, PageTextEntry> { ["1.jpg"] = new(Long, TextPlacement.OnPicture, TextBand.Bottom, ChosenByUser: true) } };
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(book));
        var model = NewMain();

        await ShowAsync(model, opened);

        Assert.Equal(TextPlacement.OnPicture, PageOf(model, "1.jpg").Story!.Placement);
        Assert.Null(model.MovedTextNotice);
        Assert.Contains("1 page has more words than fit", model.Chips);
    });

    [Fact]
    public Task APageMovedByItselfMovesBackWhenItsWordsAreCutDown() => OnPump(async () =>
    {
        var model = NewMain();
        await ShowAsync(model, Opened("back", Long, ShortWithBlanks));
        var tile = TileOf(model, "1.jpg");
        model.SelectTile(tile);

        model.StoryInput = "Pancake Polly fished a bottle out of the sea.";
        model.SelectTile(tile);

        Assert.Equal(TextPlacement.OnPicture, PageOf(model, "1.jpg").Story!.Placement);
        Assert.Null(model.MovedTextNotice);
    });

    [Fact]
    public Task FlippingTheSwitchMarksThePageAsChosenSoItIsNotMovedAgain() => OnPump(async () =>
    {
        var model = NewMain();
        await ShowAsync(model, Opened("flip", Long, ShortWithBlanks));
        model.SelectTile(TileOf(model, "1.jpg"));
        Assert.Equal(TextPlacement.OwnPage, model.SelectedPlacement);

        model.SelectedPlacement = TextPlacement.OnPicture;

        var story = PageOf(model, "1.jpg").Story!;
        Assert.Equal(TextPlacement.OnPicture, story.Placement);
        Assert.True(story.ChosenByUser);
        Assert.Null(model.MovedTextNotice);
        Assert.Contains("1 page has more words than fit", model.Chips);
    });

    [Theory]
    [InlineData(new int[0], null)]
    [InlineData(new[] { 3 }, "Moved to its own page so the words fit: page 3.")]
    [InlineData(new[] { 3, 7 }, "Moved to its own page so the words fit: pages 3, 7.")]
    public void TheNoteNamesTheMovedPages(int[] numbers, string? expected)
    {
        Assert.Equal(expected, PageNotes.MovedToOwnPage(numbers));
    }

    // ---- Back cover -----------------------------------------------------------------------------------

    [Fact]
    public Task TheBackCoverPrintsLastWithItsCreditsInTheMiddleBand() => OnPump(async () =>
    {
        var model = NewMain();

        await ShowAsync(model, Opened("back-cover", "Short one.", "Short two."));

        var pages = model.BookPages;
        Assert.Equal(PageKind.BackCover, pages[^1].Kind);
        var story = pages[^1].Story!;
        Assert.Equal(TextBand.Middle, story.EffectiveBand);
        Assert.Equal("Original story by Aesop\nRetold for young readers by Ashkan Sirous\nIllustration: Ashkan Sirous\nNovember 2026", story.Text);
        Assert.Equal(TextPlacement.OnPicture, story.Placement);
        Assert.Null(model.MovedTextNotice);
    });

    [Fact]
    public Task SwitchingTheLanguageMakesTheCreditsAgainInThatLanguage() => OnPump(async () =>
    {
        var model = NewMain();
        await ShowAsync(model, Opened("back-fa", "Short one.", "Short two."));

        model.SelectedDinoLanguage = Fa;
        await model.LanguageSwitch;

        var text = model.BookPages[^1].Story!.Text;
        Assert.StartsWith("داستان اصلی از Aesop", text);
        Assert.EndsWith("نوامبر ۲۰۲۶", text);
        Assert.Null(model.KeptTextNotice);
    });

    [Fact]
    public Task CreditsTheUserChangedAreKeptOnALanguageSwitchAndNamed() => OnPump(async () =>
    {
        var model = NewMain();
        await ShowAsync(model, Opened("back-edited", "Short one.", "Short two."));
        var tile = TileOf(model, "back cover.jpg");
        model.SelectTile(tile);
        model.StoryInput = "For my grandson.";
        model.SelectTile(tile);

        model.SelectedDinoLanguage = Fa;
        await model.LanguageSwitch;

        Assert.Equal("For my grandson.", model.BookPages[^1].Story!.Text);
        Assert.StartsWith("Back cover", model.KeptTextNotice);
    });

    [Fact]
    public Task ABookSavedBeforeItsBackCoverWasReadGetsTheCreditsOnOpen() => OnPump(async () =>
    {
        var opened = Opened("back-old", "Short one.", "Short two.");
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(new BookText { Language = Fa, Pages = { ["1.jpg"] = new("My words.") } }));
        var model = NewMain();

        await ShowAsync(model, opened);

        Assert.StartsWith("داستان اصلی از Aesop", model.BookPages[^1].Story!.Text);
        Assert.Equal("My words.", PageOf(model, "1.jpg").Story!.Text);
    });

    [Fact]
    public Task ABackCoverTheUserClearedStaysClearedWhenTheBookIsOpenedAgain() => OnPump(async () =>
    {
        var opened = Opened("back-cleared", "Short one.", "Short two.");
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(new BookText { BackCoverCleared = true, Pages = { ["1.jpg"] = new("My words.") } }));
        var model = NewMain();

        await ShowAsync(model, opened);

        Assert.Null(model.BookPages[^1].Story);
    });

    [Fact]
    public Task AnOwnPageChoiceSavedBeforeTheFieldExistedIsNotMovedBackOntoThePicture() => OnPump(async () =>
    {
        var opened = Opened("old-own", "Short one.", "Short two.");
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), """{"pages":{"1.jpg":{"text":"Short one.","placement":"OwnPage"}}}""");
        var model = NewMain();

        await ShowAsync(model, opened);

        var story = PageOf(model, "1.jpg").Story!;
        Assert.Equal(TextPlacement.OwnPage, story.Placement);
        Assert.True(story.ChosenByUser);
    });

    [Fact]
    public Task AnOldOnThePictureEntryWhoseWordsDoNotFitIsMovedByTheFit() => OnPump(async () =>
    {
        var opened = Opened("old-pic", Long, ShortWithBlanks);
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(new BookText { Pages = { ["1.jpg"] = new(Long, TextPlacement.OnPicture, TextBand.Bottom) } }));
        var model = NewMain();

        await ShowAsync(model, opened);

        Assert.Equal(TextPlacement.OwnPage, PageOf(model, "1.jpg").Story!.Placement);
    });

    [Fact]
    public Task AnEditElsewhereNeverMarksTheBackCoverAsClearedButEmptyingItDoes() => OnPump(async () =>
    {
        var opened = Opened("back-clear-flag", "Short one.", "Short two.");
        var model = NewMain();
        await ShowAsync(model, opened);
        var first = TileOf(model, "1.jpg");
        model.SelectTile(first);
        model.StoryInput = "Pip ran.";
        model.SelectTile(first);
        model.StartOver();
        Assert.False(BookTextStore.Read(opened.Folder).Book.BackCoverCleared);
        Assert.Contains("back cover.jpg", BookTextStore.Read(opened.Folder).Book.Pages.Keys);

        var second = NewMain();
        await ShowAsync(second, opened);
        var back = TileOf(second, "back cover.jpg");
        second.SelectTile(back);
        second.StoryInput = string.Empty;
        second.SelectTile(first);
        second.StartOver();

        Assert.True(BookTextStore.Read(opened.Folder).Book.BackCoverCleared);
    });

    [Fact]
    public Task CreditsAddedToAnOldBookUseTheLanguageTheSelectShowsWhenTheSavedOneIsNotInTheBook() => OnPump(async () =>
    {
        var opened = Opened("back-unlisted", "Short one.", "Short two.");
        File.WriteAllText(BookTextStore.PathIn(opened.Folder), BookTextJson.Serialize(new BookText { Language = "de", Pages = { ["1.jpg"] = new("My words.") } }));
        var model = NewMain();

        await ShowAsync(model, opened);

        Assert.StartsWith("Original story by Aesop", model.BookPages[^1].Story!.Text);
    });

    [Fact]
    public Task TheFitIsMeasuredOnlyForPagesWhoseKeyChanged() => OnPump(async () =>
    {
        var model = NewMain();
        await ShowAsync(model, Opened("fit-memo", Long, ShortWithBlanks));
        var measured = model.FitComputations;
        Assert.True(measured > 0);
        var second = TileOf(model, "2.jpg");
        model.SelectTile(second);

        model.StoryInput = "Pip ran far.";
        model.SelectTile(second);

        Assert.Equal(measured + 1, model.FitComputations);
        Assert.Equal(TextPlacement.OwnPage, PageOf(model, "1.jpg").Story!.Placement);
    });

    [Fact]
    public Task APageWhosePictureSizeIsNotLoadedYetIsNotDecidedAndNothingIsDecoded() => OnPump(async () =>
    {
        var model = NewMain();
        await ShowAsync(model, Opened("fit-unloaded", Long, ShortWithBlanks));
        Directory.CreateDirectory(_dir);
        var file = Path.Combine(_dir, "later.jpg");
        File.WriteAllBytes(file, Png());
        var page = new BookPage(file, "later", PageKind.Numbered);
        var context = new StoryContext(PaperKind.A4, 5, ScalingMode.Fit, PageNumberPosition.Off, null);
        var measured = model.FitComputations;

        var fits = model.BandFits(page, new StoryText(Long), context);

        Assert.Null(fits);
        Assert.Equal(measured, model.FitComputations);
    });

    [Fact]
    public Task APageThatBecomesUndecidedKeepsItsOwnPageAndStaysInTheMovedNotice() => OnPump(async () =>
    {
        var model = NewMain();
        await ShowAsync(model, Opened("fit-undecided", Long, ShortWithBlanks));
        Assert.Equal(TextPlacement.OwnPage, PageOf(model, "1.jpg").Story!.Placement);
        Assert.NotNull(model.MovedTextNotice);
        model.Thumbnails.Clear();

        model.Print.MarginMm += 1;

        Assert.Equal(TextPlacement.OwnPage, PageOf(model, "1.jpg").Story!.Placement);
        Assert.NotNull(model.MovedTextNotice);
    });

    [Fact]
    public Task ASizeFilledInByASynchronousLoadSchedulesTheRefreshThatDecidesThePlacement() => OnPump(async () =>
    {
        var model = NewMain();
        await ShowAsync(model, Opened("fit-sync", Long, ShortWithBlanks));
        model.Thumbnails.Clear();
        model.FitRefreshPending = false;

        model.Print.MarginMm += 1;

        Assert.True(model.FitRefreshPending);
    });

    [Fact]
    public Task EditingThePageOfAnAutomaticallyMovedPageKeepsItsPlacementAndNeverMarksItChosen() => OnPump(async () =>
    {
        var model = NewMain();
        await ShowAsync(model, Opened("fit-edit", Long, ShortWithBlanks));
        model.SelectTile(TileOf(model, "1.jpg"));
        Assert.False(PageOf(model, "1.jpg").Story!.ChosenByUser);

        model.StoryInput = Long + " The end.";

        var story = PageOf(model, "1.jpg").Story!;
        Assert.Equal(TextPlacement.OwnPage, story.Placement);
        Assert.False(story.ChosenByUser);
    });

    [Fact]
    public void ALastPageWhoseBackCoverNamesAnotherPictureStaysAnOrdinaryPage()
    {
        var story = """{"Title":"T","Language":"en","CoverFile":"c.jpg","Pages":[{"Text":"One.","ImageFile":"p1.jpg"},{"Text":"Two.","ImageFile":"p2.jpg","BackCover":{"storyBy":"Aesop","month":"2026-11","picture":"back.jpg"}}]}""";
        var png = Png();
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in new (string, byte[])[] { ("package.json", Encoding.UTF8.GetBytes("""{"schemaVersion":1,"id":"x","title":"T","language":"en"}""")), ("story.json", Encoding.UTF8.GetBytes(story)), ("c.jpg", png), ("p1.jpg", png), ("p2.jpg", png), ("back.jpg", png) })
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(bytes);
            }
        }

        stream.Position = 0;
        var book = DinoBookReader.Read(stream);

        Assert.Equal(["One.", "Two."], book.Pages.Select(p => p.Text));
        Assert.Equal("back.jpg", book.BackCover!.PictureFile);
    }

    [Fact]
    public void ThePictureLoaderFillsOneEntryWithBothThumbnailAndRealSizeAndPeekNeverDecodes()
    {
        Directory.CreateDirectory(_dir);
        var file = Path.Combine(_dir, "one.jpg");
        File.WriteAllBytes(file, Png());
        var loader = new ThumbnailLoader();

        Assert.Null(loader.Peek(file));
        Assert.Null(loader.Peek(file));
        Assert.NotNull(loader.Load(file));

        var loaded = loader.Peek(file)!;
        Assert.NotNull(loaded.Thumbnail);
        Assert.Equal(new SizeD(148, 210), loaded.RealSize);
        Assert.Equal(loaded.RealSize, loader.RealSizeOf(file));
    }

    [Fact]
    public void TheCacheKeepsTheBackCoverPictureWhenThereIsOneAndTheBlankWhenThereIsNot()
    {
        var with = DinoBookReader.PlannedPictures(DinoBookReader.Read(new MemoryStream(Package("a", "b", withBackCover: true))));
        var without = DinoBookReader.PlannedPictures(DinoBookReader.Read(new MemoryStream(Package("a", "b", withBackCover: false))));

        Assert.Equal("back cover.jpg", DinoBookCache.FileNamesOf(with)[^1]);
        Assert.DoesNotContain(DinoBookCache.BackCoverFile, DinoBookCache.FileNamesOf(with));
        Assert.Equal(DinoBookCache.BackCoverFile, DinoBookCache.FileNamesOf(without)[^1]);
    }

    // ---- Helpers --------------------------------------------------------------------------------------

    /// <summary>
    /// Runs the body with a synchronization context that works through its posted callbacks on this thread, as the app's UI thread does: without one an
    /// await continues on a pool thread and could rebuild the book while the test thread is rebuilding it too.
    /// </summary>
    private static Task OnPump(Func<Task> body)
    {
        var previous = SynchronizationContext.Current;
        var pump = new PumpContext();
        SynchronizationContext.SetSynchronizationContext(pump);
        try
        {
            pump.RunUntilDone(body());
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        return Task.CompletedTask;
    }

    private sealed class PumpContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public void RunUntilDone(Task task)
        {
            while (!task.IsCompleted)
            {
                if (_queue.TryTake(out var item, TimeSpan.FromMilliseconds(20)))
                {
                    item.Callback(item.State);
                }
            }

            task.GetAwaiter().GetResult();
        }
    }

    private static async Task ShowAsync(MainViewModel model, OpenedDino opened)
    {
        model.ShowOpened(opened);
        await model.LanguageSwitch;
    }

    private static BookPage PageOf(MainViewModel model, string file) => model.BookPages.Single(p => Path.GetFileName(p.Path) == file);

    private static PageViewModel TileOf(MainViewModel model, string file)
    {
        var page = PageOf(model, file);
        return new PageViewModel(file, null, page.Kind) { Source = page };
    }

    private static IEnumerable<PageViewModel> Tiles(SheetViewModel sheet) => [sheet.Front.Left, sheet.Front.Right, sheet.Back.Left, sheet.Back.Right];

    private MainViewModel NewMain()
    {
        var print = new PrintViewModel(new PrinterService(), new SilentPrompts(), new SettingsStore(Path.Combine(_dir, "settings.json")));
        return new MainViewModel(() => null, print, () => null);
    }

    private OpenedDino Opened(string name, string one, string two)
    {
        var folder = Path.Combine(_dir, name);
        var package = Package(one, two, withBackCover: true);
        var book = DinoBookReader.Read(new MemoryStream(package));
        var pictures = DinoBookReader.ExtractPictures(new MemoryStream(package), book, folder, TestContext.Current.CancellationToken);
        var files = new[] { pictures.CoverFile! }.Concat(pictures.PageFiles).Append(pictures.BackCoverFile!).Select(f => Path.Combine(folder, f)).ToList();
        return new OpenedDino(Path.Combine(_dir, name + ".dino"), folder, book, files, pictures);
    }

    private static byte[] Package(string one, string two, bool withBackCover)
    {
        var pages = new List<object> { new { Text = one, ImageFile = "p1.jpg" }, new { Text = two, ImageFile = "p2.jpg" } };
        if (withBackCover)
        {
            pages.Add(new { Text = "Original story by Aesop", ImageFile = "back-cover.jpg", BackCover = new { storyBy = "Aesop", retold = true, illustration = "Ashkan Sirous", month = "2026-11", picture = "back-cover.jpg" } });
        }

        var story = new { Title = "Seagull", Language = "en", CoverFile = "c.jpg", Pages = pages, Translations = new Dictionary<string, Dictionary<string, string>> { [Fa] = new() { ["Short one."] = "اول", ["Short two."] = "دوم" } } };
        var png = Png();
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = """{"schemaVersion":1,"id":"seagull","title":"Seagull","language":"en"}""";
            var files = new List<(string, byte[])> { ("package.json", Encoding.UTF8.GetBytes(manifest)), ("story.json", JsonSerializer.SerializeToUtf8Bytes(story)), ("c.jpg", png), ("p1.jpg", png), ("p2.jpg", png) };
            if (withBackCover)
            {
                files.Add(("back-cover.jpg", png));
            }

            foreach (var (name, bytes) in files)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(bytes);
            }
        }

        return stream.ToArray();
    }

    /// <summary>A real portrait JPEG (the shape of a half sheet), so the fit can be measured over it.</summary>
    private static byte[] Png()
    {
        const int width = 148;
        const int height = 210;
        var pixels = new byte[width * height * 3];
        Array.Fill(pixels, (byte)200);
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb24, null, pixels, width * 3)));
        using var memory = new MemoryStream();
        encoder.Save(memory);
        return memory.ToArray();
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
