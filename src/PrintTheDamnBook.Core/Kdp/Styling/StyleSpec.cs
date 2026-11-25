namespace PrintTheDamnBook.Core.Kdp.Styling;

/// <summary>The values of the story text rules, all from <c>docs/design/kdp-typography.md</c> (Slice 46); nothing here is chosen in the drawing code.</summary>
public static class StyleSpec
{
    /// <summary>R1: the gap between two paragraphs.</summary>
    public const double ParagraphGapPt = 14;

    /// <summary>R2 big words, R7 refrains and R8 shouts.</summary>
    public const double BigPt = 30;

    /// <summary>R2 small and quiet words.</summary>
    public const double SmallPt = 19;

    /// <summary>R2 quiet words and R10 side text.</summary>
    public const string MutedInk = "#6E6880";

    /// <summary>R9 stretched words.</summary>
    public const double StretchEm = 0.22;

    /// <summary>R4 colours, in the order of the three words.</summary>
    public static readonly IReadOnlyList<string> TripletColors = ["#C2410C", "#1F5FA8", "#7A3E9D"];

    /// <summary>R3: the contrast a colour word must keep against its page.</summary>
    public const double MinContrast = 4.5;

    /// <summary>R4: the shortest word that may be one of three alike but not equal words.</summary>
    public const int TripletMinLetters = 4;

    /// <summary>R4: how many letters at the start and at the end the three words share.</summary>
    public const int TripletSharedEdge = 2;

    /// <summary>R7: the fewest words of a refrain, and how often it must occur in the book.</summary>
    public const int RefrainMinWords = 3;

    public const int RefrainMinCount = 2;

    /// <summary>R10: a song is set bold at this size, its words alternately this many points above and below the line (so 2 × this between neighbours).</summary>
    public const double SongPt = 28;

    public const double SongBouncePt = 2;

    /// <summary>R6: baseline moves are measured and drawn for lines of this size only (Slice 46 item A), so a shape that moves a word needs it and its neighbours at this size.</summary>
    public const double ShiftLinePt = 24;

    /// <summary>R8: the most words of a shout.</summary>
    public const int ShoutMaxWords = 3;
}
