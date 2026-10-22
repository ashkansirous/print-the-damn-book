namespace PrintTheDamnBook.App.Printing;

public sealed record PrinterChoice(string Name, bool IsDefault)
{
    /// <summary>What the printer list shows.</summary>
    public string Label => IsDefault ? Name + " (default)" : Name;
}
