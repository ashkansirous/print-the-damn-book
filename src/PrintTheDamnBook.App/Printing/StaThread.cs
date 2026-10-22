namespace PrintTheDamnBook.App.Printing;

/// <summary>Runs work on a fresh single-threaded-apartment thread, which WPF documents and print queues require.</summary>
internal static class StaThread
{
    public static Task<T> Run<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => Complete(completion, work)) { IsBackground = true, Name = "Print worker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    public static Task Run(Action work)
    {
        return Run(() =>
        {
            work();
            return true;
        });
    }

    private static void Complete<T>(TaskCompletionSource<T> completion, Func<T> work)
    {
        try
        {
            completion.SetResult(work());
        }
        catch (Exception ex)
        {
            completion.SetException(ex);
        }
    }
}
