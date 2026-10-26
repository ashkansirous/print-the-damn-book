using System.IO;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.App.Services;

public enum BookTextSaveState
{
    /// <summary>Nothing changed since the book was opened.</summary>
    Idle,
    Saving,
    Saved,
    Failed,

    /// <summary>The file in the folder is from a newer version (or could not be read), so it is left alone.</summary>
    Locked,
}

/// <summary>
/// <c>book.json</c> in the picture folder: reads it when the folder opens and writes it a moment after the last change. Reading
/// never throws and never blocks opening the folder (a missing or broken file reads as no text); saving is debounced and runs
/// off the UI thread, and <see cref="Flush"/> writes what is pending at once.
/// </summary>
public sealed class BookTextStore : IDisposable
{
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);

    private readonly string _path;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly object _gate = new();
    private CancellationTokenSource? _timer;
    private readonly object _writeGate = new();
    private BookText? _pending;
    private long _pendingSequence;
    private long _latestSequence;
    private long _writtenSequence;
    private readonly bool _canSave;
    private bool _disposed;

    /// <summary>Completes when every write started so far has finished, so <see cref="Flush"/> can wait for one the delay already took.</summary>
    private Task _writesInFlight = Task.CompletedTask;

    public BookTextStore(string folder, bool canSave)
    {
        _path = Path.Combine(folder, BookText.FileName);
        _canSave = canSave;
        State = canSave ? BookTextSaveState.Idle : BookTextSaveState.Locked;
    }

    public event EventHandler? StateChanged;

    public BookTextSaveState State { get; private set; }

    public static string PathIn(string folder) => Path.Combine(folder, BookText.FileName);

    /// <summary>Reads the folder's <c>book.json</c>; safe to call off the UI thread.</summary>
    public static BookTextLoad Read(string folder) => BookTextJson.Load(PathIn(folder));

    /// <summary>Remembers this snapshot as the one to write after the delay; a newer call replaces it.</summary>
    public void Schedule(BookText snapshot)
    {
        if (!_canSave || _disposed)
        {
            return;
        }

        CancellationTokenSource timer;
        lock (_gate)
        {
            _pending = snapshot;
            _pendingSequence = ++_latestSequence;
            _timer?.Cancel();
            _timer?.Dispose();
            _timer = timer = new CancellationTokenSource();
        }

        SetState(BookTextSaveState.Saving);
        _ = WriteAfterDelayAsync(timer.Token);
    }

    /// <summary>Writes the pending snapshot now, if there is one.</summary>
    public void Flush()
    {
        BookText? snapshot;
        long sequence;
        Task inFlight;
        lock (_gate)
        {
            _timer?.Cancel();
            snapshot = _pending;
            sequence = _pendingSequence;
            _pending = null;
            inFlight = _writesInFlight;
        }

        if (snapshot is not null)
        {
            Write(snapshot, sequence);
        }

        WaitFor(inFlight);
    }

    private static void WaitFor(Task writes)
    {
        try
        {
            writes.Wait();
        }
        catch (AggregateException)
        {
            // Write failures are reported through the save state; waiting only has to outlast them.
        }
    }

    public void Dispose()
    {
        Flush();
        _disposed = true;
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private async Task WriteAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SaveDelay, token);
            await TakeAndWriteAsync(token);
        }
        catch (OperationCanceledException)
        {
            // A newer change or a flush replaced this timer.
        }
        catch (Exception)
        {
            // Nobody observes this task, so any failure must reach the user as a failed save, not stay "Saving...".
            SetState(BookTextSaveState.Failed);
        }
    }

    private Task TakeAndWriteAsync(CancellationToken token)
    {
        lock (_gate)
        {
            if (token.IsCancellationRequested || _pending is not { } snapshot)
            {
                return Task.CompletedTask;
            }

            var sequence = _pendingSequence;
            _pending = null;
            var write = Task.Run(() => Write(snapshot, sequence), CancellationToken.None);
            _writesInFlight = Task.WhenAll(_writesInFlight, write);
            return write;
        }
    }

    /// <summary>Writes one snapshot. Writes are serialised and a snapshot older than one already written is dropped; the state only says Saved when nothing newer is waiting.</summary>
    private void Write(BookText snapshot, long sequence)
    {
        bool saved;
        lock (_writeGate)
        {
            if (sequence < _writtenSequence)
            {
                return;
            }

            saved = BookTextJson.TrySave(_path, snapshot);
            _writtenSequence = sequence;
        }

        bool newerWaiting;
        lock (_gate)
        {
            newerWaiting = _latestSequence > sequence;
        }

        if (!saved || !newerWaiting)
        {
            SetState(saved ? BookTextSaveState.Saved : BookTextSaveState.Failed);
        }
    }

    private void SetState(BookTextSaveState state)
    {
        State = state;
        if (_context is null)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _context.Post(_ => StateChanged?.Invoke(this, EventArgs.Empty), null);
        }
    }
}
