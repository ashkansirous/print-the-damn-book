namespace PrintTheDamnBook.App.Printing;

/// <summary>A printing problem whose message is fit to show the user as is.</summary>
/// <param name="MayHavePrinted">True when the job had already been handed to the spooler, so some pages may have printed.</param>
public sealed class PrintFailedException(string message, Exception? inner = null, bool mayHavePrinted = false) : Exception(message, inner)
{
    public bool MayHavePrinted { get; } = mayHavePrinted;
}
