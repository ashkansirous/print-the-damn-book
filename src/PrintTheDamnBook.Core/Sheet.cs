namespace PrintTheDamnBook.Core;

/// <summary>One printed face of a sheet: two pages side by side on a landscape sheet.</summary>
public sealed record SheetFace(BookPage Left, BookPage Right);

/// <summary>One physical sheet, printed duplex: <see cref="Front"/> and <see cref="Back"/>.</summary>
public sealed record Sheet(int Number, SheetFace Front, SheetFace Back);
