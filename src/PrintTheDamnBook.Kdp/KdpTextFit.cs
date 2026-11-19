using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Layout;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PrintTheDamnBook.Kdp;

/// <summary>
/// Measures whether words fit their box. QuestPDF has no measuring call, so the words are set in a one-page document of the box's size with
/// <c>ShowEntire</c>, which throws when they overflow. The measuring page has the interior's exact page size and the same box, so QuestPDF's
/// pixel snapping of the page size is the same in the measurement and the drawing; the same engine that draws the page breaks the lines, the way the App's painter measures
/// and Core chooses (<see cref="StoryTextLayout.Choose"/>).
/// </summary>
internal static class KdpTextFit
{
    public static bool Fits(string text, string family, double pt, RectD boxInches, bool rightToLeft)
    {
        var document = Document.Create(container => container.Page(page =>
        {
            page.Size((float)KdpTrim.PageWidthInches, (float)KdpTrim.PageHeightInches, Unit.Inch);
            page.Margin(0);
            page.Content().Element(body => KdpInteriorDocument.InBox(body, boxInches).AlignMiddle().ShowEntire().Element(words => Words(words, text, family, pt, rightToLeft)));
        }));

        try
        {
            document.GeneratePdf(Stream.Null);
            return true;
        }
        catch (DocumentLayoutException)
        {
            return false;
        }
    }

    /// <summary>The words as the page sets them: centred, the story font and ink, the page line height.</summary>
    public static void Words(IContainer body, string text, string family, double pt, bool rightToLeft)
    {
        (rightToLeft ? body.ContentFromRightToLeft() : body).Text(t =>
        {
            t.AlignCenter();
            t.DefaultTextStyle(s => s.FontFamily(family).FontSize((float)pt).LineHeight((float)StoryTextLayout.PageLineHeight).FontColor(KdpInteriorDocument.Ink));
            t.Span(text);
        });
    }
}
