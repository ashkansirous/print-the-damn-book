namespace PrintTheDamnBook.App.Printing;

public enum DuplexMode
{
    Automatic,
    Manual,
}

/// <summary>An entry of the Two-sided picker.</summary>
public sealed record DuplexChoice(DuplexMode Mode, string Label)
{
    public static IReadOnlyList<DuplexChoice> All { get; } =
    [
        new(DuplexMode.Automatic, "Automatic (printer)"),
        new(DuplexMode.Manual, "Manual (flip the stack yourself)"),
    ];
}
