using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using PrintTheDamnBook.App.Printing;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.App.Settings;
using PrintTheDamnBook.App.ViewModels;
using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.App.Tests;

/// <summary>Slice 20: Dino open and store hardening, tested through the view models, the store client and the cache.</summary>
public sealed class DinoOpenHardeningTests : IDisposable
{
    private const string Manifest = """{"schemaVersion":1,"id":"abc123","title":"The Pigs","language":"en"}""";
    private const string Story = """{"Title":"The Pigs","Language":"en","CoverFile":"cover-1.jpg","Pages":[{"Text":"Pip.","ImageFile":"page-1.jpg"}]}""";
    private const string Heading = "I couldn't reach Dino's store";

    private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "app-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    // ---- PlaceAlternate -------------------------------------------------------------------------------

    [Fact]
    public void PlaceAlternateCopiesSavedWordsWhenItCan()
    {
        var (temp, folder) = TempAndDamagedFolders(saved: "{}");

        var placement = DinoBookCache.PlaceAlternate(temp, folder);

        Assert.True(placement.SavedWordsKept);
        Assert.True(File.Exists(Path.Combine(placement.Folder, "book.json")));
    }

    [Fact]
    public void PlaceAlternateStillOpensWhenBookJsonIsLocked()
    {
        var (temp, folder) = TempAndDamagedFolders(saved: "{}");

        DinoBookCache.Placement placement;
        using (new FileStream(Path.Combine(folder, "book.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            placement = DinoBookCache.PlaceAlternate(temp, folder);
        }

        Assert.False(placement.SavedWordsKept);
        Assert.True(Directory.Exists(placement.Folder));
        Assert.False(File.Exists(Path.Combine(placement.Folder, "book.json")));
        Assert.True(placement.Created);
    }

    private (string Temp, string Folder) TempAndDamagedFolders(string saved)
    {
        var temp = Path.Combine(_dir, "book.tmp-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(_dir, "book-0123456789abcdef");
        Directory.CreateDirectory(temp);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "book.json"), saved);
        return (temp, folder);
    }

    // ---- Hand-over stash ------------------------------------------------------------------------------

    [Fact]
    public async Task StartOverClearsAStashedHandoverAlert()
    {
        var model = NewMain();
        model.HandoverProblemFile = "from-dino.dino";

        var load = model.LoadDinoAsync(Path.Combine(_dir, "missing.dino"));
        Assert.Equal("from-dino.dino", model.StashedHandoverProblem);
        model.StartOver();
        await load;

        Assert.Null(model.StashedHandoverProblem);
        Assert.Null(model.HandoverProblemFile);
    }

    // ---- What a failed open does to a download --------------------------------------------------------

    [Fact]
    public async Task OnlyAMalformedFileIsRejected()
    {
        Directory.CreateDirectory(_dir);
        var damaged = Path.Combine(_dir, "damaged.dino");
        File.WriteAllBytes(damaged, [1, 2, 3]);

        Assert.Equal(DinoLoadResult.Rejected, await NewMain().LoadDinoAsync(damaged));
        Assert.Equal(DinoLoadResult.Failed, await NewMain().LoadDinoAsync(Path.Combine(_dir, "missing.dino")));
    }

    [Fact]
    public async Task AValidBookWithNoPrintableLanguageIsKeptAndShowsThePlainFailure()
    {
        Directory.CreateDirectory(_dir);
        using var client = new DinoStoreClient(_dir, new StubHandler(_ => throw new InvalidOperationException()));
        var path = Path.Combine(_dir, "he-v1.dino");
        File.WriteAllBytes(path, GoodPackage("he"));
        var model = NewMain();

        var result = await model.LoadDinoAsync(path);
        client.Settle(path, result);

        Assert.Equal(DinoLoadResult.Failed, result);
        Assert.True(File.Exists(path));
        Assert.False(model.IsLoaded);
        Assert.Contains("language I can't print yet", model.Status);
    }

    [Theory]
    [InlineData(DinoLoadResult.Rejected, false)]
    [InlineData(DinoLoadResult.Failed, true)]
    [InlineData(DinoLoadResult.Skipped, true)]
    public void SettleDeletesTheDownloadOnlyWhenRejected(DinoLoadResult result, bool kept)
    {
        using var client = new DinoStoreClient(_dir, new StubHandler(_ => throw new InvalidOperationException()));
        var path = Path.Combine(_dir, "a1-v1.dino");
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(path, [1]);

        client.Settle(path, result);

        Assert.Equal(kept, File.Exists(path));
    }

    [Fact]
    public void SettleAfterOpeningV2KeepsV3AndRemovesV1()
    {
        using var client = new DinoStoreClient(_dir, new StubHandler(_ => throw new InvalidOperationException()));
        Directory.CreateDirectory(_dir);
        foreach (var version in new[] { 1, 2, 3 })
        {
            File.WriteAllBytes(Path.Combine(_dir, $"a1-v{version}.dino"), [1]);
        }

        client.Settle(Path.Combine(_dir, "a1-v2.dino"), DinoLoadResult.Opened);

        Assert.Equal(["a1-v2.dino", "a1-v3.dino"], Directory.GetFiles(_dir).Select(f => Path.GetFileName(f)!).Order().ToArray());
    }

    // ---- Reusing a download ---------------------------------------------------------------------------

    [Fact]
    public async Task ATruncatedExistingDownloadIsFetchedAgain()
    {
        var calls = 0;
        using var client = new DinoStoreClient(_dir, new StubHandler(_ => { calls++; return Ok(GoodPackage()); }));
        var entry = Entry("a1", 1);
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(DinoStoreFiles.FinalPath(_dir, entry), GoodPackage().Take(40).ToArray());

        var path = await client.DownloadAsync(entry, new Progress<DownloadProgress>(), TestContext.Current.CancellationToken);

        Assert.Equal(1, calls);
        using var file = File.OpenRead(path);
        Assert.Equal("The Pigs", DinoBookReader.Read(file).Title);
    }

    [Fact]
    public async Task AGoodExistingDownloadIsReusedWithoutTheNetwork()
    {
        var calls = 0;
        using var client = new DinoStoreClient(_dir, new StubHandler(_ => { calls++; return Ok(GoodPackage()); }));
        var entry = Entry("a1", 1);
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(DinoStoreFiles.FinalPath(_dir, entry), GoodPackage());

        await client.DownloadAsync(entry, new Progress<DownloadProgress>(), TestContext.Current.CancellationToken);

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task AnUnreadableExistingDownloadIsNotDeletedWhenTheFreshOneCannotReplaceIt()
    {
        var calls = 0;
        using var client = new DinoStoreClient(_dir, new StubHandler(_ => { calls++; return Ok(GoodPackage()); }));
        var entry = Entry("a1", 1);
        Directory.CreateDirectory(_dir);
        var final = DinoStoreFiles.FinalPath(_dir, entry);
        File.WriteAllBytes(final, GoodPackage());

        using (new FileStream(final, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var ex = await Assert.ThrowsAsync<DinoStoreException>(() => client.DownloadAsync(entry, new Progress<DownloadProgress>(), TestContext.Current.CancellationToken));
            Assert.Equal(DinoStreamCopier.InUseMessage, ex.Message);
        }

        Assert.Equal(1, calls);
        Assert.True(File.Exists(final));
    }

    [Fact]
    public async Task AHandoverWhoseOpenThrowsAnIoErrorShowsTheHandoverAlert()
    {
        var model = NewMain();

        await model.LoadHandoverAsync(new HandoverRequest(Path.Combine(_dir, "gone.dino"), null, true));

        Assert.Equal("gone.dino", model.HandoverProblemFile);
    }

    [Fact]
    public async Task ANonNetworkInnerExceptionShowsTheOtherHeadingWithoutBlamingTheConnection()
    {
        var source = new FakeSource { Download = () => throw new DinoStoreException("Dino's store download did not finish.", new InvalidOperationException()) };
        var model = await ListedAsync(source);

        await model.ChooseAsync(model.Cards[0]);

        Assert.NotEqual(Heading, model.FailureHeading);
        Assert.DoesNotContain("reach", model.FailureReason);
    }

    // ---- The store dialog ----------------------------------------------------------------------------

    [Fact]
    public async Task ACancelRacingAnIoErrorIsStillACancel()
    {
        var source = new FakeSource();
        var model = await ListedAsync(source);
        source.Download = () => { model.Cancel(); throw new IOException("aborted"); };

        await model.ChooseAsync(model.Cards[0]);

        Assert.Equal(DinoStoreState.List, model.State);
        Assert.Equal("Download cancelled.", model.Note);
    }

    [Fact]
    public async Task ACancelRacingAnHttpErrorIsStillACancel()
    {
        var source = new FakeSource();
        var model = await ListedAsync(source);
        source.Download = () => { model.Cancel(); throw new HttpRequestException("aborted"); };

        await model.ChooseAsync(model.Cards[0]);

        Assert.Equal(DinoStoreState.List, model.State);
        Assert.Equal("Download cancelled.", model.Note);
    }

    [Fact]
    public async Task ADownloadThatFinishesAtCancelSaysOnlyThatItWasCancelled()
    {
        var source = new FakeSource();
        var model = await ListedAsync(source);
        source.Download = () => { model.Cancel(); return Task.FromResult("a1-v1.dino"); };

        await model.ChooseAsync(model.Cards[0]);

        Assert.Equal("Download cancelled.", model.Note);
        Assert.DoesNotContain("Nothing was kept", model.Note);
    }

    [Fact]
    public async Task ADiskFailureShowsItsReasonAndABackToListButton()
    {
        var source = new FakeSource { Download = () => throw new DinoStoreException(DinoStreamCopier.DiskMessage, new DinoDiskException("full")) };
        var model = await ListedAsync(source);

        await model.ChooseAsync(model.Cards[0]);

        Assert.Equal(DinoStoreState.Failed, model.State);
        Assert.Equal(DinoStreamCopier.DiskMessage, model.FailureReason);
        Assert.NotEqual(Heading, model.FailureHeading);
        Assert.DoesNotContain("internet", model.FailureHint);
        Assert.True(model.CanGoBack);

        model.BackToLastListCommand.Execute(null);

        Assert.Equal(DinoStoreState.List, model.State);
        Assert.Equal(string.Empty, model.Note);
    }

    [Fact]
    public async Task ANetworkFailureStillBlamesTheConnection()
    {
        var source = new FakeSource { Download = () => throw new DinoStoreException("Could not reach Dino's store.", new HttpRequestException("down")) };
        var model = await ListedAsync(source);

        await model.ChooseAsync(model.Cards[0]);

        Assert.Equal(Heading, model.FailureHeading);
        Assert.Contains("internet", model.FailureHint);
    }

    [Fact]
    public async Task AFailedListHasNoListToGoBackTo()
    {
        var model = new DinoStoreViewModel(new FakeSource { Catalog = () => throw new DinoStoreException("Could not reach Dino's store.") });

        await model.LoadAsync();

        Assert.Equal(DinoStoreState.Failed, model.State);
        Assert.False(model.CanGoBack);
    }

    [Fact]
    public async Task AFailedListDoesNotTellTheUserToGoBackToTheList()
    {
        var model = new DinoStoreViewModel(new FakeSource { Catalog = () => throw new DinoBookException("That list is too large.") });

        await model.LoadAsync();

        Assert.False(model.CanGoBack);
        Assert.DoesNotContain("go back", model.FailureHint);
    }

    [Theory]
    [InlineData(DinoStreamCopier.TimedOut)]
    [InlineData(DinoStreamCopier.CutShort)]
    public async Task AStalledOrCutShortDownloadBlamesTheConnection(string message)
    {
        var source = new FakeSource { Download = () => throw new DinoBookException(message) };
        var model = await ListedAsync(source);

        await model.ChooseAsync(model.Cards[0]);

        Assert.Equal(Heading, model.FailureHeading);
        Assert.Contains("internet", model.FailureHint);
    }

    [Fact]
    public async Task ADamagedDownloadThatCannotBeDeletedIsNotReused()
    {
        var calls = 0;
        using var client = new DinoStoreClient(_dir, new StubHandler(_ => { calls++; return Ok(GoodPackage()); }));
        var entry = Entry("a1", 1);
        Directory.CreateDirectory(_dir);
        var final = DinoStoreFiles.FinalPath(_dir, entry);
        File.WriteAllBytes(final, GoodPackage().Take(40).ToArray());

        await using (new FileStream(final, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var ex = await Assert.ThrowsAsync<DinoStoreException>(() => client.DownloadAsync(entry, new Progress<DownloadProgress>(), TestContext.Current.CancellationToken));
            Assert.IsNotType<DinoDiskException>(ex.InnerException);
            Assert.Contains("another window", ex.Message);
        }

        Assert.Equal(0, calls);
        Assert.Equal(40, new FileInfo(final).Length);
    }

    [Fact]
    public async Task AStoreRefusalDoesNotBlameTheConnection()
    {
        var source = new FakeSource { Download = () => throw new DinoStoreException("Dino's store refused that request.") };
        var model = await ListedAsync(source);

        await model.ChooseAsync(model.Cards[0]);

        Assert.Equal(DinoStoreState.Failed, model.State);
        Assert.NotEqual(Heading, model.FailureHeading);
        Assert.DoesNotContain("internet", model.FailureHint);
    }

    [Fact]
    public async Task AnHttpStatusFailureDoesNotBlameTheConnection()
    {
        var source = new FakeSource { Download = () => throw new DinoStoreException("x", new HttpRequestException("gone", null, HttpStatusCode.NotFound)) };
        var model = await ListedAsync(source);

        await model.ChooseAsync(model.Cards[0]);

        Assert.NotEqual(Heading, model.FailureHeading);
    }

    [Fact]
    public async Task ADroppedNetworkStreamAndATimeoutBlameTheConnection()
    {
        var inners = new Exception[] { new DinoNetworkException("reset", new IOException("reset")), new OperationCanceledException(), new HttpRequestException("down") };
        foreach (var inner in inners)
        {
            var source = new FakeSource { Download = () => throw new DinoStoreException("x", inner) };
            var model = await ListedAsync(source);

            await model.ChooseAsync(model.Cards[0]);

            Assert.Equal(Heading, model.FailureHeading);
        }
    }

    [Fact]
    public async Task ALocalIoOrAccessFailureNeverBlamesTheConnection()
    {
        var failures = new Exception[]
        {
            new IOException("path too long"),
            new DinoStoreException("x", new IOException("locked")),
            new DinoStoreException("x", new UnauthorizedAccessException()),
            new DinoStoreException("x", new DinoDiskException("full", new IOException("disk"))),
            new DinoDiskException("full"),
        };
        foreach (var failure in failures)
        {
            var source = new FakeSource { Download = () => throw failure };
            var model = await ListedAsync(source);

            await model.ChooseAsync(model.Cards[0]);

            Assert.Equal(DinoStoreState.Failed, model.State);
            Assert.NotEqual(Heading, model.FailureHeading);
            Assert.DoesNotContain("internet", model.FailureHint);
        }
    }

    [Fact]
    public async Task ABareIoFailureShowsThePlainFailureText()
    {
        var source = new FakeSource { Download = () => throw new IOException("path too long") };
        var model = await ListedAsync(source);

        await model.ChooseAsync(model.Cards[0]);

        Assert.Equal("Something went wrong with Dino's store.", model.FailureReason);
    }

    [Fact]
    public async Task ADroppedResponseIsANetworkFailureAndABlockedStoreFolderIsADiskFailure()
    {
        using var network = new DinoStoreClient(Path.Combine(_dir, "net"), new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new DroppingStream()) }));
        var dropped = await Assert.ThrowsAsync<DinoStoreException>(() => network.DownloadAsync(Entry("a1", 1), new Progress<DownloadProgress>(), TestContext.Current.CancellationToken));
        Assert.IsType<DinoNetworkException>(dropped.InnerException);

        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "a file where the store folder should be");
        using var disk = new DinoStoreClient(blocker, new StubHandler(_ => Ok(GoodPackage())));
        var full = await Assert.ThrowsAsync<DinoStoreException>(() => disk.DownloadAsync(Entry("a1", 1), new Progress<DownloadProgress>(), TestContext.Current.CancellationToken));
        Assert.IsType<DinoDiskException>(full.InnerException);
    }

    [Fact]
    public async Task ATimeoutOnAnUncancelledTokenIsAConnectionFailure()
    {
        using var client = new DinoStoreClient(_dir, new StubHandler(_ => throw new TaskCanceledException("timed out")));
        var source = new FakeSource { Download = () => client.DownloadAsync(Entry("a1", 1), new Progress<DownloadProgress>(), CancellationToken.None) };
        var model = await ListedAsync(source);

        await model.ChooseAsync(model.Cards[0]);

        Assert.Equal(DinoStoreState.Failed, model.State);
        Assert.Equal(Heading, model.FailureHeading);
    }

    [Fact]
    public async Task ARetryAfterAFailedListReloadsTheListNotThePreviousBook()
    {
        var catalogCalls = 0;
        var downloads = 0;
        var source = new FakeSource
        {
            Catalog = () => ++catalogCalls == 2 ? throw new DinoStoreException("Could not reach Dino's store.", new HttpRequestException("down")) : null,
            Download = () => { downloads++; throw new DinoStoreException(DinoStreamCopier.DiskMessage, new DinoDiskException("full")); },
        };
        var model = await ListedAsync(source);
        await model.ChooseAsync(model.Cards[0]);
        Assert.True(model.CanGoBack);

        await model.LoadAsync();
        Assert.Equal(DinoStoreState.Failed, model.State);
        Assert.False(model.CanGoBack);
        Assert.Equal(Heading, model.FailureHeading);

        await model.RetryAsync();

        Assert.Equal(3, catalogCalls);
        Assert.Equal(1, downloads);
        Assert.Equal(DinoStoreState.List, model.State);
        Assert.False(model.CanGoBack);
    }

    [Fact]
    public async Task AFailureLeavesNothingBehindOnceTheListIsShownAgain()
    {
        var source = new FakeSource { Download = () => throw new DinoStoreException(DinoStreamCopier.DiskMessage, new DinoDiskException("full")) };
        var model = await ListedAsync(source);
        await model.ChooseAsync(model.Cards[0]);

        model.BackToLastListCommand.Execute(null);

        Assert.False(model.CanGoBack);
        Assert.Equal(string.Empty, model.FailureReason);
        Assert.Equal(Heading, model.FailureHeading);
    }

    [Fact]
    public async Task ACardWithNoCoverUrlIsNotQueuedForACoverRetry()
    {
        var source = new FakeSource { Catalog = () => DinoStoreCatalog.Parse("""{"stories":[{"id":"a1","title":"T","language":"en","version":1}]}""") };
        var model = await ListedAsync(source);
        await model.StopAsync();

        Assert.Equal(1, source.CoverCalls);
    }

    // ---- The saved-words notice -----------------------------------------------------------------------

    [Fact]
    public void TheNoticeSaysWhenSavedWordsCouldNotBeCarriedOver()
    {
        var book = new DinoBook("a1", "T", "en", null, [], new Dictionary<string, IReadOnlyDictionary<string, string>>());
        var kept = new OpenedDino("a.dino", _dir, book, [], new DinoPictures(null, []));
        var lost = kept with { SavedWordsKept = false };

        Assert.Null(MainViewModel.NoticeOf(kept, null));
        Assert.Contains("saved words could not be carried over", MainViewModel.NoticeOf(lost, null));
        Assert.Contains("saved words could not be carried over", MainViewModel.NoticeOf(lost, "a language notice"));
        Assert.Contains("a language notice", MainViewModel.NoticeOf(lost, "a language notice"));
    }

    [Fact]
    public async Task ReusingADownloadHonoursCancel()
    {
        using var client = new DinoStoreClient(_dir, new StubHandler(_ => Ok(GoodPackage())));
        var entry = Entry("a1", 1);
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(DinoStoreFiles.FinalPath(_dir, entry), GoodPackage());
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DownloadAsync(entry, new Progress<DownloadProgress>(), cancel.Token));
    }

    [Fact]
    public void PlaceAlternateStillOpensWhenBookJsonIsDeniedAccess()
    {
        var (temp, folder) = TempAndDamagedFolders(saved: "{}");
        var target = Path.Combine(temp, "book.json");
        File.WriteAllText(target, "{}");
        File.SetAttributes(target, FileAttributes.ReadOnly);

        var placement = DinoBookCache.PlaceAlternate(temp, folder);
        File.SetAttributes(Path.Combine(placement.Folder, "book.json"), FileAttributes.Normal);

        Assert.False(placement.SavedWordsKept);
        Assert.True(Directory.Exists(placement.Folder));
    }

    [Fact]
    public async Task ACoverThatFailsIsTriedOnceMore()
    {
        var source = new FakeSource();
        var model = await ListedAsync(source);
        await model.StopAsync();

        Assert.Equal(2, source.CoverCalls);
    }

    // ---- Helpers --------------------------------------------------------------------------------------

    private MainViewModel NewMain()
    {
        var print = new PrintViewModel(new PrinterService(), new SilentPrompts(), new SettingsStore(Path.Combine(_dir, "settings.json")));
        return new MainViewModel(() => null, print, () => null);
    }

    private static DinoStoreEntry Entry(string id, int version)
    {
        return new DinoStoreEntry(id, "Title", "en", version, 3, 4, 7, null, new Uri($"https://dinoreader.sirous.uk/api/stories/{id}/download"));
    }

    private static async Task<DinoStoreViewModel> ListedAsync(FakeSource source)
    {
        var model = new DinoStoreViewModel(source);
        await model.LoadAsync();
        Assert.Equal(DinoStoreState.List, model.State);
        return model;
    }

    private static byte[] GoodPackage(string language = "en")
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in new[] { ("package.json", Encoding.UTF8.GetBytes(Manifest.Replace("\"en\"", $"\"{language}\""))), ("story.json", Encoding.UTF8.GetBytes(Story.Replace("\"en\"", $"\"{language}\""))), ("cover-1.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }), ("page-1.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }) })
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(bytes);
            }
        }

        return stream.ToArray();
    }

    private static HttpResponseMessage Ok(byte[] body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }

    private sealed class DroppingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new IOException("connection reset");
        }
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

    private sealed class FakeSource : IDinoStoreSource
    {
        public Func<DinoStoreCatalog?>? Catalog { get; init; }

        public Func<Task<string>> Download { get; set; } = () => Task.FromResult("x.dino");

        public int CoverCalls { get; private set; }

        public Task<DinoStoreCatalog> LoadCatalogAsync(CancellationToken cancel)
        {
            return Task.FromResult(Catalog?.Invoke() ?? DinoStoreCatalog.Parse("""{"stories":[{"id":"a1","title":"T","language":"en","version":1,"coverUrl":"/api/stories/a1/cover"}]}"""));
        }

        public Task<string?> FetchCoverAsync(DinoStoreEntry entry, CancellationToken cancel)
        {
            CoverCalls++;
            return Task.FromResult<string?>(null);
        }

        public void DiscardCover(DinoStoreEntry entry)
        {
        }

        public Task<string> DownloadAsync(DinoStoreEntry entry, IProgress<DownloadProgress> progress, CancellationToken cancel)
        {
            return Download();
        }
    }
}
