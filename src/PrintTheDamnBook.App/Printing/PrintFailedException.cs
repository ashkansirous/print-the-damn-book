namespace PrintTheDamnBook.App.Printing;

/// <summary>A printing problem whose message is fit to show the user as is.</summary>
public sealed class PrintFailedException(string message, Exception? inner = null) : Exception(message, inner);
