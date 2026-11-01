using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Tests;

public class DinoStoreTransferTests : IDisposable
{
    private static readonly TimeSpan Patient = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Impatient = TimeSpan.FromMilliseconds(60);

    private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "store-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static DinoStoreEntry Entry(string id, int version)
    {
        var url = new Uri($"https://dinoreader.sirous.uk/api/stories/{id}/download");
        return new DinoStoreEntry(id, "T", "en", version, null, null, null, null, url);
    }

    private static CopyLimits Limits(long cap, long? total = null, TimeSpan? stall = null) => new(cap, total, stall ?? Patient);

    private static IProgress<DownloadProgress> NoProgress() => new Progress<DownloadProgress>();

    [Fact]
    public void CheckLengthRefusesAClaimedSizeOverTheCap()
    {
        Assert.Throws<DinoBookException>(() => DinoStreamCopier.CheckLength(101, 100));
        DinoStreamCopier.CheckLength(100, 100);
        DinoStreamCopier.CheckLength(null, 100);
    }

    [Fact]
    public async Task CopyStopsWhenMoreBytesArriveThanTheCapEvenIfTheServerSaidLess()
    {
        using var source = new MemoryStream(new byte[300_000]);
        using var target = new MemoryStream();

        var ex = await Assert.ThrowsAsync<DinoBookException>(
            () => DinoStreamCopier.CopyAsync(source, target, Limits(100_000, total: 10), NoProgress(), CancellationToken.None));

        Assert.Equal(DinoStreamCopier.TooLarge, ex.Message);
        Assert.True(target.Length <= 100_000);
    }

    [Fact]
    public async Task CopyTakesExactlyTheCapAndReportsProgress()
    {
        var data = new byte[200_000];
        Random.Shared.NextBytes(data);
        using var source = new MemoryStream(data);
        using var target = new MemoryStream();
        var seen = new List<DownloadProgress>();

        await DinoStreamCopier.CopyAsync(source, target, Limits(data.Length, total: data.Length), new SyncProgress(seen), CancellationToken.None);

        Assert.Equal(data, target.ToArray());
        Assert.Equal(data.Length, seen[^1].Received);
        Assert.Equal(data.Length, seen[^1].Total);
    }

    [Fact]
    public async Task CopyRefusesABodyShorterThanTheServerSaid()
    {
        using var source = new MemoryStream(new byte[500]);
        using var target = new MemoryStream();

        var ex = await Assert.ThrowsAsync<DinoBookException>(
            () => DinoStreamCopier.CopyAsync(source, target, Limits(100_000, total: 1000), NoProgress(), CancellationToken.None));

        Assert.Equal(DinoStreamCopier.CutShort, ex.Message);
    }

    [Fact]
    public async Task CopyGivesUpOnAStreamThatGoesQuiet()
    {
        using var source = new StallingStream();
        using var target = new MemoryStream();

        var ex = await Assert.ThrowsAsync<DinoBookException>(
            () => DinoStreamCopier.CopyAsync(source, target, Limits(100, stall: Impatient), NoProgress(), CancellationToken.None));

        Assert.Equal(DinoStreamCopier.TimedOut, ex.Message);
    }

    [Fact]
    public async Task CopyReportsTheCallersOwnCancelAsCancel()
    {
        using var source = new StallingStream();
        using var target = new MemoryStream();
        using var cancel = new CancellationTokenSource(Impatient);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DinoStreamCopier.CopyAsync(source, target, Limits(100), NoProgress(), cancel.Token));
    }

    [Fact]
    public async Task CopyNamesATargetFailureADiskProblem()
    {
        using var source = new MemoryStream(new byte[10]);

        await Assert.ThrowsAsync<DinoDiskException>(
            () => DinoStreamCopier.CopyAsync(source, new ThrowingTarget(), Limits(100), NoProgress(), CancellationToken.None));
    }

    [Fact]
    public async Task AWholeWriteIsRenamedIntoPlaceAndLeavesNoPartFile()
    {
        var final = Path.Combine(_dir, "a1-v1.dino");

        await DinoStoreFiles.WriteAtomicallyAsync(final, s => s.WriteAsync(new byte[] { 1, 2, 3 }).AsTask());

        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(final, TestContext.Current.CancellationToken));
        Assert.Equal([final], Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task AFailedWriteLeavesNeitherAPartFileNorTheFinalFile()
    {
        var final = Path.Combine(_dir, "a1-v1.dino");

        await Assert.ThrowsAsync<InvalidOperationException>(() => DinoStoreFiles.WriteAtomicallyAsync(final, async s =>
        {
            await s.WriteAsync(new byte[] { 9 });
            throw new InvalidOperationException();
        }));

        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task TwoWritesOfTheSameFileDoNotShareAPartFile()
    {
        var final = Path.Combine(_dir, "a1-v1.dino");
        var second = Task.CompletedTask;

        await DinoStoreFiles.WriteAtomicallyAsync(final, async s =>
        {
            await s.WriteAsync(new byte[] { 1 });
            second = DinoStoreFiles.WriteAtomicallyAsync(final, t => t.WriteAsync(new byte[] { 2 }).AsTask());
            await second;
        });

        Assert.Equal([final], Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task ACancelledWriteLeavesNoPartFile()
    {
        var final = Path.Combine(_dir, "a1-v1.dino");
        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DinoStoreFiles.WriteAtomicallyAsync(final, async s =>
        {
            await s.WriteAsync(new byte[] { 9 });
            await cancel.CancelAsync();
            cancel.Token.ThrowIfCancellationRequested();
        }));

        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task AFailedWriteKeepsAnEarlierFinalFile()
    {
        var final = Path.Combine(_dir, "a1-v1.dino");
        await DinoStoreFiles.WriteAtomicallyAsync(final, s => s.WriteAsync(new byte[] { 1 }).AsTask());

        await Assert.ThrowsAsync<InvalidOperationException>(() => DinoStoreFiles.WriteAtomicallyAsync(final, _ => throw new InvalidOperationException()));

        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(final, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnUnopenableFolderIsADiskProblem()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        await File.WriteAllTextAsync(blocker, "a file where a folder should be", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DinoDiskException>(
            () => DinoStoreFiles.WriteAtomicallyAsync(Path.Combine(blocker, "a1-v1.dino"), s => s.WriteAsync(new byte[] { 1 }).AsTask()));
    }

    [Fact]
    public void PathsFollowTheIdAndVersion()
    {
        var entry = Entry("a1", 3);

        Assert.Equal(Path.Combine("r", "a1-v3.dino"), DinoStoreFiles.FinalPath("r", entry));
        Assert.Equal(Path.Combine("r", "a1-v3.dino.part"), DinoStoreFiles.PartPath(DinoStoreFiles.FinalPath("r", entry)));
        Assert.Equal(Path.Combine("r", "covers", "a1-v3.jpg"), DinoStoreFiles.CoverPath("r", entry));
    }

    [Fact]
    public void OtherVersionsListsOnlyTheSameBooksOlderFiles()
    {
        var keep = Path.Combine("r", "a1-v3.dino");
        var files = new[]
        {
            keep, Path.Combine("r", "a1-v2.dino"), Path.Combine("r", "A1-v1.dino"),
            Path.Combine("r", "a10-v1.dino"), Path.Combine("r", "b2-v1.dino"), Path.Combine("r", "a1-v2.dino.part"),
        };

        var others = DinoStoreFiles.OtherVersions(files, keep);

        Assert.Equal([Path.Combine("r", "a1-v2.dino"), Path.Combine("r", "A1-v1.dino")], others);
    }

    [Fact]
    public void OtherCoverVersionsListsOnlyTheSameBooksOlderCovers()
    {
        var keep = Path.Combine("r", "covers", "a1-v3.jpg");
        var files = new[] { keep, Path.Combine("r", "covers", "a1-v2.jpg"), Path.Combine("r", "covers", "a1-v1.jpg"), Path.Combine("r", "covers", "a1-v2-v1.jpg"), Path.Combine("r", "covers", "b2-v1.jpg") };

        var others = DinoStoreFiles.OtherCoverVersions(files, keep);

        Assert.Equal(new[] { files[1], files[2] }, others);
    }

    [Fact]
    public async Task AUniquePartNameLeavesNothingBehind()
    {
        var final = Path.Combine(_dir, "covers", "a1-v1.jpg");

        await DinoStoreFiles.WriteAtomicallyAsync(final, s => s.WriteAsync(new byte[] { 1 }).AsTask());

        Assert.Equal([final], Directory.GetFiles(Path.GetDirectoryName(final)!));
    }

    [Fact]
    public void OtherVersionsOfAnUnrecognisedNameIsEmpty()
    {
        Assert.Empty(DinoStoreFiles.OtherVersions([Path.Combine("r", "x.dino")], Path.Combine("r", "picked.dino")));
    }

    private sealed class SyncProgress(List<DownloadProgress> seen) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value)
        {
            seen.Add(value);
        }
    }

    /// <summary>A stream whose reads never return until they are cancelled.</summary>
    private sealed class StallingStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    private sealed class ThrowingTarget : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new IOException("disk full");
        }
    }
}
