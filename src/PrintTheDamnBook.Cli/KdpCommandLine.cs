using System.Globalization;
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
    public const string OffsetOption = "--offset";
    public const string DefaultLanguage = "en";

    public const string Usage = "Usage: print-the-damn-book kdp <book.dino> [--language en] [--offset <page>=<pixels> ...] --out <folder>   (--offset <page>=0 clears a page's saved offset)";

    public static KdpArguments Parse(IReadOnlyList<string> args)
    {
        string? dino = null;
        string? language = null;
        string? output = null;
        var offsets = new Dictionary<int, int>();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case LanguageOption or OutOption or OffsetOption when i + 1 >= args.Count:
                    return Fail($"{args[i]} needs a value.");
                case LanguageOption or OutOption or OffsetOption when args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    return Fail($"{args[i]} needs a value, not the option {args[i + 1]}.");
                case LanguageOption:
                    language = args[++i];
                    break;
                case OutOption:
                    output = args[++i];
                    break;
                case OffsetOption when !TryParseOffset(args[i + 1], out _, out _):
                    return Fail($"{OffsetOption} needs <page>=<pixels> (a page from 1, pixels 0 or more), not {args[i + 1]}.");
                case OffsetOption:
                    TryParseOffset(args[++i], out var offsetPage, out var offsetPixels);
                    offsets[offsetPage] = offsetPixels;
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

        return output is null
            ? Fail($"{OutOption} is needed.")
            : new KdpArguments(new KdpExportRequest(dino, language ?? DefaultLanguage, output, offsets.Count > 0 ? offsets : null), null);
    }

    private static bool TryParseOffset(string text, out int page, out int pixels)
    {
        page = 0;
        pixels = 0;
        var parts = text.Split('=');
        const NumberStyles digits = NumberStyles.None;
        return parts.Length == 2
            && int.TryParse(parts[0], digits, CultureInfo.InvariantCulture, out page)
            && page >= 1
            && int.TryParse(parts[1], digits, CultureInfo.InvariantCulture, out pixels);
    }

    private static KdpArguments Fail(string error) => new(null, error);
}
