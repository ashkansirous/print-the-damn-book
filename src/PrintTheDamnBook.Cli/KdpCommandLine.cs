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
    public const string TextOption = "--text";
    public const string DinoTextOption = "--dino-text";
    public const string ClearValue = "clear";
    public const string DefaultLanguage = "en";

    public const string Usage = "Usage: print-the-damn-book kdp <book.dino> [--language en] [--offset <page|front|back>=<pixels> ...] [--text <front|back>=<left>,<top>,<width>,<height>,<text #RRGGBB>,<shadow #RRGGBB> ...] [--dino-text back=<text #RRGGBB>,<shadow #RRGGBB>] [--previews] [--crop-sheet] --out <folder>   (--offset <key>=0 clears a picture's saved offset; --text sets the box of the cover words as fractions 0 to 1 of the trim panel and their colours, --text <key>=clear and --dino-text back=clear remove the choice; --previews also writes contact-sheet.png and small page PNGs; --crop-sheet writes crop/sheet.png and crop/<key>.png, every picture whole with its kept window outlined, for the crop agent)";

    private const int TextParts = 6;
    private const int DinoParts = 2;

    public static KdpArguments Parse(IReadOnlyList<string> args)
    {
        string? dino = null;
        string? language = null;
        string? output = null;
        var offsets = new Dictionary<int, int>();
        var coverOffsets = new Dictionary<string, int>();
        var coverText = new Dictionary<string, KdpCoverText?>();
        KdpDinoTextChange? dinoText = null;
        var previews = false;
        var cropSheet = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case LanguageOption or OutOption or OffsetOption or TextOption or DinoTextOption when i + 1 >= args.Count:
                    return Fail($"{args[i]} needs a value.");
                case LanguageOption or OutOption or OffsetOption or TextOption or DinoTextOption when args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    return Fail($"{args[i]} needs a value, not the option {args[i + 1]}.");
                case TextOption when !TryParseText(args[i + 1], out _, out _):
                    return Fail($"{TextOption} needs front=<left>,<top>,<width>,<height>,<text #RRGGBB>,<shadow #RRGGBB> (or back=..., or <key>={ClearValue}); the box is fractions from 0 to 1 of the trim panel inside it, not {args[i + 1]}.");
                case TextOption:
                    TryParseText(args[++i], out var textKey, out var text);
                    coverText[textKey!] = text;
                    break;
                case DinoTextOption when !TryParseDino(args[i + 1], out _):
                    return Fail($"{DinoTextOption} needs back=<text #RRGGBB>,<shadow #RRGGBB> (or back={ClearValue}), not {args[i + 1]}.");
                case DinoTextOption:
                    TryParseDino(args[++i], out dinoText);
                    break;
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
            : new KdpArguments(new KdpExportRequest(dino, language ?? DefaultLanguage, output, offsets.Count > 0 ? offsets : null, previews, coverOffsets.Count > 0 ? coverOffsets : null, cropSheet, coverText.Count > 0 ? coverText : null, dinoText), null);
    }

    /// <summary><c>front=l,t,w,h,#text,#shadow</c> or <c>front=clear</c>: a cover key and its text choice (null when clearing).</summary>
    private static bool TryParseText(string text, out string? coverKey, out KdpCoverText? choice)
    {
        coverKey = null;
        choice = null;
        var parts = text.Split('=');
        if (parts.Length != 2 || !KdpPictureKeys.IsCover(parts[0]))
        {
            return false;
        }

        coverKey = parts[0];
        if (parts[1] == ClearValue)
        {
            return true;
        }

        var fields = parts[1].Split(',');
        if (fields.Length != TextParts || !TryBox(fields[..4], out var box) || !KdpHexColor.TryNormalize(fields[4], out var color) || !KdpHexColor.TryNormalize(fields[5], out var shadow))
        {
            return false;
        }

        choice = new KdpCoverText(box!, color, shadow);
        return true;
    }

    private static bool TryBox(string[] fields, out KdpTextBox? box)
    {
        var numbers = new double[fields.Length];
        box = null;
        for (var i = 0; i < fields.Length; i++)
        {
            if (!double.TryParse(fields[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return false;
            }
        }

        var candidate = new KdpTextBox(numbers[0], numbers[1], numbers[2], numbers[3]);
        box = candidate.IsValid ? candidate : null;
        return box is not null;
    }

    /// <summary><c>back=#text,#shadow</c> or <c>back=clear</c>.</summary>
    private static bool TryParseDino(string text, out KdpDinoTextChange? change)
    {
        change = null;
        var parts = text.Split('=');
        if (parts.Length != 2 || parts[0] != KdpPictureKeys.Back)
        {
            return false;
        }

        if (parts[1] == ClearValue)
        {
            change = new KdpDinoTextChange(null);
            return true;
        }

        var colors = parts[1].Split(',');
        if (colors.Length != DinoParts || !KdpHexColor.TryNormalize(colors[0], out var color) || !KdpHexColor.TryNormalize(colors[1], out var shadow))
        {
            return false;
        }

        change = new KdpDinoTextChange(new KdpTextColors(color, shadow));
        return true;
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
