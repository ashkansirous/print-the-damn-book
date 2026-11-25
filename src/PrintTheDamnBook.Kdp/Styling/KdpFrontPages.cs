using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Kdp.Styling;

/// <summary>One speaking character of the Dino page: the name and its R5 colour.</summary>
internal sealed record KdpSpeakerName(string Name, string Color);

/// <summary>
/// What the redesigned front and back pages need beyond the wording (Slice 46 item G): the round title picture (a JPEG square cut from a story picture), the title with R3
/// applied and split into its two lines, the tints of the title page and of The End (the tints of the first and of the last story text page), The End's colour (the main
/// character's) and the speaking characters of the Dino page.
/// </summary>
internal sealed record KdpFrontPages(
    byte[] TitleCircle,
    TitleLines Title,
    Rgb? TitleTint,
    Rgb? EndTint,
    string EndColor,
    IReadOnlyList<KdpSpeakerName> Speakers);
