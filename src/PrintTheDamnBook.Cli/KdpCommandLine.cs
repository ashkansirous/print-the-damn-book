using PrintTheDamnBook.Kdp;

namespace PrintTheDamnBook.Cli;

/// <summary>The arguments of <c>print-the-damn-book kdp</c>, or why they are wrong.</summary>
public sealed record KdpArguments(KdpExportRequest? Request, string? Error);

/// <summary>Parses the arguments of the <c>kdp</c> command; nothing else lives here (the work is in the Kdp project).</summary>
public static class KdpCommandLine
{
    public const string Command = "kdp";
    public const string LanguageOption = "--language";
    public const string OutOption = "--out";
    public const string DefaultLanguage = "en";

    public const string Usage = "Usage: print-the-damn-book kdp <book.dino> [--language en] --out <folder>";

    public static KdpArguments Parse(IReadOnlyList<string> args)
    {
        string? dino = null;
        string? language = null;
        string? output = null;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case LanguageOption or OutOption when i + 1 >= args.Count:
                    return Fail($"{args[i]} needs a value.");
                case LanguageOption or OutOption when args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    return Fail($"{args[i]} needs a value, not the option {args[i + 1]}.");
                case LanguageOption:
                    language = args[++i];
                    break;
                case OutOption:
                    output = args[++i];
                    break;
                case var option when option.StartsWith("--", StringComparison.Ordinal):
                    return Fail($"Unknown option {option}.");
                case var path when dino is null:
                    dino = path;
                    break;
                default:
                    return Fail($"Unexpected argument {args[i]}.");
            }
        }

        if (dino is null)
        {
            return Fail("A .dino file is needed.");
        }

        return output is null ? Fail($"{OutOption} is needed.") : new KdpArguments(new KdpExportRequest(dino, language ?? DefaultLanguage, output), null);
    }

    private static KdpArguments Fail(string error) => new(null, error);
}
