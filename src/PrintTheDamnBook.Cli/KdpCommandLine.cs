using System.Globalization;
using PrintTheDamnBook.Core.Kdp;
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
    public const string PreviewsOption = "--previews";
    public const string CropSheetOption = "--crop-sheet";
    public const string DefaultLanguage = "en";

    public const string Usage = "Usage: print-the-damn-book kdp <book.dino> [--language en] [--offset <page|front|back>=<pixels> ...] [--previews] [--crop-sheet] --out <folder>   (--offset <key>=0 clears a picture's saved offset; --previews also writes contact-sheet.png and small page PNGs; --crop-sheet writes crop/sheet.png and crop/<key>.png, every picture whole with its kept window outlined, for the crop agent)";

    public static KdpArguments Parse(IReadOnlyList<string> args)
    {
        string? dino = null;
        string? language = null;
        string? output = null;
        var offsets = new Dictionary<int, int>();
        var coverOffsets = new Dictionary<string, int>();
        var previews = false;
        var cropSheet = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case LanguageOption or OutOption or OffsetOption when i + 1 >= args.Count:
                    return Fail($"{args[i]} needs a value.");
                case LanguageOption or OutOption or OffsetOption when args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    return Fail($"{args[i]} needs a value, not the option {args[i + 1]}.");
                case PreviewsOption:
                    previews = true;
                    break;
                case CropSheetOption:
                    cropSheet = true;
                    break;
                case LanguageOption:
                    language = args[++i];
                    break;
                case OutOption:
                    output = args[++i];
                    break;
                case OffsetOption when !TryParseOffset(args[i + 1], out _, out _, out _):
                    return Fail($"{OffsetOption} needs <page>=<pixels> or front=<pixels> or back=<pixels> (a page from 1, pixels 0 or more), not {args[i + 1]}.");
                case OffsetOption:
                    TryParseOffset(args[++i], out var key, out var offsetPage, out var offsetPixels);
                    if (key is not null)
                    {
                        coverOffsets[key] = offsetPixels;
                    }
                    else
                    {
                        offsets[offsetPage] = offsetPixels;
                    }

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
            : new KdpArguments(new KdpExportRequest(dino, language ?? DefaultLanguage, output, offsets.Count > 0 ? offsets : null, previews, coverOffsets.Count > 0 ? coverOffsets : null, cropSheet), null);
    }

    /// <summary>The picture is a story page number (<paramref name="page"/>) or a cover key (<paramref name="coverKey"/>); the pixels are 0 or more.</summary>
    private static bool TryParseOffset(string text, out string? coverKey, out int page, out int pixels)
    {
        coverKey = null;
        page = 0;
        pixels = 0;
        var parts = text.Split('=');
        return parts.Length == 2
            && KdpPictureKeys.TryParse(parts[0], out page, out coverKey)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out pixels);
    }

    private static KdpArguments Fail(string error) => new(null, error);
}
