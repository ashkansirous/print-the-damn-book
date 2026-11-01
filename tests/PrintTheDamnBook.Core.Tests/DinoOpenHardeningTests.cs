using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Tests;

/// <summary>Slice 20: the store-file rules that were too loose (newer versions, a lost move race, messages).</summary>
public class DinoOpenHardeningTests : IDisposable
{
    private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "hardening-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void OpeningV2WhileV3IsOnDiskKeepsV3AndRemovesV1()
    {
        var keep = Path.Combine("r", "a1-v2.dino");
        var files = new[] { keep, Path.Combine("r", "a1-v3.dino"), Path.Combine("r", "a1-v1.dino"), Path.Combine("r", "a1-v10.dino"), Path.Combine("r", "b2-v1.dino") };

        var others = DinoStoreFiles.OtherVersions(files, keep);

        Assert.Equal([Path.Combine("r", "a1-v1.dino")], others);
    }

    [Fact]
    public async Task ALostMoveRaceCountsAsSuccessWhenTheTargetIsThere()
    {
        var final = Path.Combine(_dir, "a1-v1.dino");
        Directory.CreateDirectory(_dir);
        await File.WriteAllBytesAsync(final, [1, 2, 3], TestContext.Current.CancellationToken);

        await using (new FileStream(final, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await DinoStoreFiles.WriteAtomicallyAsync(final, s => s.WriteAsync(new byte[] { 1, 2, 3 }).AsTask(), TestContext.Current.CancellationToken);
        }

        Assert.Equal([final], Directory.GetFiles(_dir));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(final, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACancelledLostMoveRaceStopsComparingAndLeavesNoPart()
    {
        var final = Path.Combine(_dir, "a1-v1.dino");
        Directory.CreateDirectory(_dir);
        await File.WriteAllBytesAsync(final, [1, 2, 3], TestContext.Current.CancellationToken);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await using (new FileStream(final, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DinoStoreFiles.WriteAtomicallyAsync(final, s => s.WriteAsync(new byte[] { 1, 2, 3 }).AsTask(), cancel.Token));
        }

        Assert.Equal([final], Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task ALockedDifferentFileInTheWayIsAFailureAndLeavesNoPart()
    {
        var final = Path.Combine(_dir, "a1-v1.dino");
        Directory.CreateDirectory(_dir);
        await File.WriteAllBytesAsync(final, [1, 2, 3], TestContext.Current.CancellationToken);

        await using (new FileStream(final, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var ex = await Assert.ThrowsAsync<DinoFileInUseException>(() => DinoStoreFiles.WriteAtomicallyAsync(final, s => s.WriteAsync(new byte[] { 9 }).AsTask(), TestContext.Current.CancellationToken));
            Assert.Equal(DinoStreamCopier.InUseMessage, ex.Message);
        }

        Assert.Equal([final], Directory.GetFiles(_dir));
    }

    [Fact]
    public void ANewerCoverIsNeverListedForDeletion()
    {
        var keep = Path.Combine("r", "covers", "a1-v2.jpg");
        var files = new[] { keep, Path.Combine("r", "covers", "a1-v3.jpg"), Path.Combine("r", "covers", "a1-v1.jpg"), Path.Combine("r", "covers", "b2-v1.jpg") };

        Assert.Equal([Path.Combine("r", "covers", "a1-v1.jpg")], DinoStoreFiles.OtherCoverVersions(files, keep));
    }

    [Fact]
    public void ZeroPagesIsLeftOutOfTheCardLine()
    {
        var url = new Uri("https://dinoreader.sirous.uk/api/stories/a1/download");

        Assert.Equal("ages 4–7", DinoStoreCatalog.MetaText(new DinoStoreEntry("a1", "T", "en", 1, 0, 4, 7, null, url)));
        Assert.Equal("1 page", DinoStoreCatalog.MetaText(new DinoStoreEntry("a1", "T", "en", 1, 1, null, null, null, url)));
    }

    [Fact]
    public async Task AnOversizedBodyReportsTheMessageItWasGiven()
    {
        const string message = "That cover is too large.";
        var ex = Assert.Throws<DinoBookException>(() => DinoStreamCopier.CheckLength(101, 100, message));
        Assert.Equal(message, ex.Message);

        var limits = new CopyLimits(2, null, TimeSpan.FromSeconds(10), message);
        var copied = await Assert.ThrowsAsync<DinoBookException>(() => DinoStreamCopier.CopyAsync(new MemoryStream([1, 2, 3]), new MemoryStream(), limits, new Progress<DownloadProgress>(), CancellationToken.None));
        Assert.Equal(message, copied.Message);
    }
}
