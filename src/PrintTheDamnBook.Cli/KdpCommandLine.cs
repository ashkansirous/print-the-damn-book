using System.Globalization;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Kdp;
using PrintTheDamnBook.Kdp.Styling;

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
    public const string StyleSheetOption = "--style-sheet";
    public const string TextOption = "--text";
    public const string DinoTextOption = "--dino-text";
    public const string OtherBooksOption = "--other-books";
    public const string BooksOutOption = "--books-out";
    public const string SpeakerOption = "--speaker";
    public const string MainCharacterOption = "--main-character";
    public const string TitlePictureOption = "--title-picture";
    public const string ClearValue = "clear";
    public const string DefaultLanguage = "en";

    public const string Usage = "Usage: print-the-damn-book kdp <book.dino> [--language en] [--offset <page|front|back>=<pixels> ...] [--text <front|back>=<left>,<top>,<width>,<height>,<text #RRGGBB>,<shadow #RRGGBB> ...] [--dino-text back=[<left>,<top>,<width>,<height>,]<text #RRGGBB>,<shadow #RRGGBB>] [--other-books <folder of .dino files> [--books-out <folder holding each book's output folder>]] [--speaker \"<cast name>=<#RRGGBB>\" ...] [--main-character \"<cast name>[=<reason>]\"] [--title-picture <page>=<centre x>,<centre y>,<diameter>] [--previews] [--crop-sheet] [--style-sheet] --out <folder>   (--style-sheet writes styles/speakers.md (each speaker with its lines, pages and colour, the page tints) and the crop/<key>.png copies for the style agent; --speaker \"<name>=clear\", --main-character clear and --title-picture clear remove a style choice; --speaker sets the colour of a cast member's quotes (dark enough on white and on the page tints, not red, unlike the other speakers' colours and the triplet colours), --title-picture the round title picture: its centre as fractions 0 to 1 of the picture's width and height, its diameter as a fraction of the picture's shorter side; --offset <key>=0 clears a picture's saved offset; --text sets the box of the cover words as fractions 0 to 1 of the trim panel and their colours, --text <key>=clear and --dino-text back=clear remove the choice; --dino-text with a box places the QR code and Dino line there, without one it sets the colours only; --other-books puts up to 15 of the other books' covers on the last page, their saved choices read from <books-out>/*/kdp.json (default: the parent of --out); --previews also writes contact-sheet.png and small page PNGs; --crop-sheet writes crop/sheet.png and crop/<key>.png, every picture whole with its kept window outlined, for the crop agent)";

    private const int TextParts = 6;
    private const int DinoColorParts = 2;
    private const int BoxFields = 4;

    public static KdpArguments Parse(IReadOnlyList<string> args)
    {
        string? dino = null;
        string? language = null;
        string? output = null;
        var offsets = new Dictionary<int, int>();
        var coverOffsets = new Dictionary<string, int>();
        var coverText = new Dictionary<string, KdpCoverText?>();
        KdpDinoTextChange? dinoText = null;
        string? otherBooks = null;
        string? booksOut = null;
        var speakers = new Dictionary<string, string?>();
        KdpMainCharacterChange? mainCharacter = null;
        KdpTitlePictureChange? titlePicture = null;
        var previews = false;
        var cropSheet = false;
        var styleSheet = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case LanguageOption or OutOption or OffsetOption or TextOption or DinoTextOption or OtherBooksOption or BooksOutOption or SpeakerOption or MainCharacterOption or TitlePictureOption when i + 1 >= args.Count:
                    return Fail($"{args[i]} needs a value.");
                case LanguageOption or OutOption or OffsetOption or TextOption or DinoTextOption or OtherBooksOption or BooksOutOption or SpeakerOption or MainCharacterOption or TitlePictureOption when args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    return Fail($"{args[i]} needs a value, not the option {args[i + 1]}.");
                case SpeakerOption when !TryParseSpeaker(args[i + 1], out _, out _, out var speakerError):
                    return Fail($"{SpeakerOption} needs \"<cast name>=#RRGGBB\" (or \"<cast name>={ClearValue}\"): {speakerError}");
                case SpeakerOption:
                    TryParseSpeaker(args[++i], out var speakerName, out var speakerColor, out _);
                    speakers[speakerName!] = speakerColor;
                    break;
                case MainCharacterOption:
                    mainCharacter = ParseMainCharacter(args[++i]);
                    break;
                case TitlePictureOption when !TryParseTitlePicture(args[i + 1], out _):
                    return Fail($"{TitlePictureOption} needs <page>=<centre x>,<centre y>,<diameter> (a page from 1, then the centre as fractions 0 to 1 of the picture's width and height, the diameter as a fraction above 0 of its shorter side), or {ClearValue}, not {args[i + 1]}.");
                case TitlePictureOption:
                    TryParseTitlePicture(args[++i], out titlePicture);
                    break;
                case OtherBooksOption:
                    otherBooks = args[++i];
                    break;
                case BooksOutOption:
                    booksOut = args[++i];
                    break;
                case TextOption when !TryParseText(args[i + 1], out _, out _):
                    return Fail($"{TextOption} needs front=<left>,<top>,<width>,<height>,<text #RRGGBB>,<shadow #RRGGBB> (or back=..., or <key>={ClearValue}); the box is fractions from 0 to 1 of the trim panel inside it, not {args[i + 1]}.");
                case TextOption:
                    TryParseText(args[++i], out var textKey, out var text);
                    coverText[textKey!] = text;
                    break;
                case DinoTextOption when !TryParseDino(args[i + 1], out _):
                    return Fail($"{DinoTextOption} needs back=<left>,<top>,<width>,<height>,<text #RRGGBB>,<shadow #RRGGBB> or back=<text #RRGGBB>,<shadow #RRGGBB> (or back={ClearValue}); the box is fractions from 0 to 1 of the trim panel inside it, not {args[i + 1]}.");
                case DinoTextOption:
                    TryParseDino(args[++i], out dinoText);
                    break;
                case PreviewsOption:
                    previews = true;
                    break;
                case CropSheetOption:
                    cropSheet = true;
                    break;
                case StyleSheetOption:
                    styleSheet = true;
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

        if (booksOut is not null && otherBooks is null)
        {
            return Fail($"{BooksOutOption} is only used together with {OtherBooksOption}.");
        }

        return output is null
            ? Fail($"{OutOption} is needed.")
            : new KdpArguments(new KdpExportRequest(dino, language ?? DefaultLanguage, output, offsets.Count > 0 ? offsets : null, previews, coverOffsets.Count > 0 ? coverOffsets : null, cropSheet, coverText.Count > 0 ? coverText : null, dinoText, otherBooks, booksOut, StyleChanges(speakers, mainCharacter, titlePicture), styleSheet), null);
    }

    private static KdpStyleChanges? StyleChanges(Dictionary<string, string?> speakers, KdpMainCharacterChange? mainCharacter, KdpTitlePictureChange? titlePicture)
    {
        var changes = new KdpStyleChanges(speakers.Count > 0 ? speakers : null, mainCharacter, titlePicture);
        return changes.IsEmpty ? null : changes;
    }

    /// <summary><c>Seagull=#1F4FA0</c> or <c>Seagull=clear</c>: a cast name and its colour (null when clearing). A colour that fails the rules checkable without the book is refused with the reason.</summary>
    private static bool TryParseSpeaker(string text, out string? name, out string? color, out string? error)
    {
        name = null;
        color = null;
        error = null;
        var cut = text.LastIndexOf('=');
        if (cut <= 0 || cut == text.Length - 1)
        {
            error = $"not {text}.";
            return false;
        }

        name = text[..cut].Trim();
        var value = text[(cut + 1)..];
        if (value.Trim().Equals(ClearValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        error = SpeakerColourRules.Check(value, [], []);
        color = error is null && KdpHexColor.TryNormalize(value, out var hex) ? hex : null;
        return error is null;
    }

    /// <summary><c>Seagull</c> or <c>Seagull=She talks on most pages</c>, or <c>clear</c>: the main character and the agent's reason.</summary>
    private static KdpMainCharacterChange ParseMainCharacter(string text)
    {
        if (text == ClearValue)
        {
            return new KdpMainCharacterChange(null);
        }

        var cut = text.IndexOf('=');
        return cut < 0 ? new KdpMainCharacterChange(text.Trim()) : new KdpMainCharacterChange(text[..cut].Trim(), text[(cut + 1)..].Trim());
    }

    /// <summary><c>7=0.5,0.4,0.6</c> (page, centre x, centre y, diameter) or <c>clear</c>.</summary>
    private static bool TryParseTitlePicture(string text, out KdpTitlePictureChange? change)
    {
        change = null;
        if (text == ClearValue)
        {
            change = new KdpTitlePictureChange(null);
            return true;
        }

        var parts = text.Split('=');
        var fields = parts.Length == 2 ? parts[1].Split(',') : [];
        var numbers = new double[fields.Length];
        if (fields.Length != 3 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var page) || page < 1
            || fields.Where((f, i) => !double.TryParse(f, NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i])).Any())
        {
            return false;
        }

        var picture = new KdpTitlePicture(page, numbers[0], numbers[1], numbers[2]);
        if (numbers.Any(n => n is < 0 or > 1) || numbers[2] <= 0)
        {
            return false;
        }

        change = new KdpTitlePictureChange(picture);
        return true;
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
        if (fields.Length != TextParts || !TryBox(fields[..BoxFields], out var box) || !KdpHexColor.TryNormalize(fields[4], out var color) || !KdpHexColor.TryNormalize(fields[5], out var shadow))
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

    /// <summary><c>back=#text,#shadow</c>, <c>back=l,t,w,h,#text,#shadow</c> (Slice 41: the box of the QR code and line) or <c>back=clear</c>.</summary>
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

        return TryParseDinoFields(parts[1].Split(','), out change);
    }

    /// <summary>The comma fields of a <c>--dino-text</c> value: two colours, or a box and two colours.</summary>
    private static bool TryParseDinoFields(string[] fields, out KdpDinoTextChange? change)
    {
        change = null;
        var boxed = fields.Length == TextParts;
        if (!boxed && fields.Length != DinoColorParts)
        {
            return false;
        }

        var first = boxed ? BoxFields : 0;
        if (!KdpHexColor.TryNormalize(fields[first], out var color) || !KdpHexColor.TryNormalize(fields[first + 1], out var shadow))
        {
            return false;
        }

        KdpTextBox? box = null;
        if (boxed && !TryBox(fields[..BoxFields], out box))
        {
            return false;
        }

        change = new KdpDinoTextChange(new KdpTextColors(color, shadow), box);
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
