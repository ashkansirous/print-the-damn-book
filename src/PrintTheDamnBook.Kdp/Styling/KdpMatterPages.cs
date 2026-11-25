using System.Globalization;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PrintTheDamnBook.Kdp.Styling;

/// <summary>
/// The drawing of the redesigned front and back pages (Slice 46 item G, ★ <c>Matter.dc.html</c> and <c>DinoLink.dc.html</c>): title, credits, belongs-to, The End and the Dino
/// page. Every size and colour is in <see cref="KdpMatterSpec"/>; each page sits in the trim box of its own side so the bleed never moves it.
/// </summary>
internal static class KdpMatterPages
{
    private const string NonBreakingSpace = " ";
    private const string NameSeparator = "   ";

    public static void Title(IContainer body, KdpPage page, KdpFrontPages front, bool rightToLeft)
    {
        Frame(body, page, rightToLeft).AlignMiddle().Column(column =>
        {
            column.Spacing((float)KdpTitleCircle.GapToTitlePt);
            column.Item().AlignCenter().Element(circle => RoundPicture(circle, front.TitleCircle));
            column.Item().Element(lines => TitleText(lines, front.Title));
        });
    }

    private static void RoundPicture(IContainer circle, byte[] jpeg)
    {
        var d = (float)KdpTitleCircle.DiameterPt;
        var shadow = new BoxShadowStyle { OffsetX = 0, OffsetY = (float)KdpTitleCircle.ShadowOffsetPt, Blur = (float)KdpTitleCircle.ShadowBlurPt, Color = Colors.Black.WithAlpha((float)KdpTitleCircle.ShadowOpacity) };
        circle.Width(d).Height(d).Shadow(shadow).CornerRadius(d / 2)
            .Border((float)KdpTitleCircle.BorderPt).BorderColor(Colors.White).BorderAlignmentInside()
            .Image(jpeg).UseOriginalImage().FitUnproportionally();
    }

    private static void TitleText(IContainer container, TitleLines title)
    {
        container.Column(column =>
        {
            if (title.Head.Count > 0)
            {
                column.Item().Element(line => TitleLine(line, title.Head));
            }

            column.Item().Element(line => TitleLine(line, title.Last));
        });
    }

    private static void TitleLine(IContainer line, IReadOnlyList<StyledRun> runs)
    {
        var family = StoryFont.For(string.Concat(runs.Select(r => r.Text)));
        line.Text(t =>
        {
            t.AlignCenter();
            t.DefaultTextStyle(s => s.FontFamily(family).LineHeight((float)TitleLines.LineHeight).FontColor(KdpInteriorDocument.Ink));
            foreach (var run in runs)
            {
                var span = t.Span(run.Text).FontSize((float)run.Look.Pt).FontColor(run.Look.Color);
                if (run.Look.Bold || run.Look.Pt >= TitleLines.LastPt)
                {
                    span.Bold();
                }
            }
        });
    }

    public static void Credits(IContainer body, KdpPage page, KdpFrontMatter matter, bool rightToLeft)
    {
        Frame(body, page, rightToLeft).PaddingBottom((float)KdpMatterSpec.CreditsBottomPt).AlignBottom().Column(column =>
        {
            column.Spacing((float)KdpMatterSpec.CreditsGroupGapPt);
            foreach (var role in matter.Roles)
            {
                column.Item().Element(group => CreditGroup(group, role.Label, role.Name));
            }

            column.Item().Element(small => SmallLines(small, matter));
        });
    }

    private static void CreditGroup(IContainer group, string? label, string name)
    {
        group.Column(column =>
        {
            column.Spacing((float)KdpMatterSpec.CreditsLabelGapPt);
            if (label is not null)
            {
                column.Item().Element(item => Centered(item, label, KdpMatterSpec.CreditsLabelPt, KdpMatterSpec.MutedInk, bold: false));
            }

            column.Item().Element(item => Centered(item, name, KdpMatterSpec.CreditsNamePt, KdpInteriorDocument.Ink, bold: true));
        });
    }

    private static void SmallLines(IContainer small, KdpFrontMatter matter)
    {
        var lines = new[] { matter.DateLine, matter.RightsLine }.Where(l => l.Length > 0).ToList();
        small.Text(t =>
        {
            t.AlignCenter();
            t.DefaultTextStyle(s => s.FontSize((float)KdpMatterSpec.CreditsSmallPt).FontColor(KdpMatterSpec.MutedInk).LineHeight((float)KdpMatterSpec.CreditsSmallLineHeight));
            t.Span(string.Join('\n', lines));
        });
    }

    public static void BelongsTo(IContainer body, KdpPage page, KdpFrontMatter matter, bool rightToLeft)
    {
        var inset = (float)KdpMatterSpec.FrameInsetPt;
        Frame(body, page, rightToLeft).Padding(inset).Layers(layers =>
        {
            layers.Layer().Svg(size => DashedBox(size, KdpMatterSpec.FrameBorderPt, KdpMatterSpec.FrameRadiusPt, KdpMatterSpec.FrameColor, dash: 9, gap: 6));
            layers.PrimaryLayer().Padding((float)(KdpMatterSpec.FramePaddingPt + KdpMatterSpec.FrameBorderPt)).AlignMiddle().Column(column =>
            {
                column.Spacing((float)KdpMatterSpec.BelongsItemGapPt);
                column.Item().Element(item => Centered(item, matter.DedicationHeading, KdpMatterSpec.BelongsHeadingPt, KdpInteriorDocument.Ink, bold: true));
                column.Item().AlignCenter().Width((float)KdpMatterSpec.NameLineWidthPt).Height((float)KdpMatterSpec.NameLineHeightPt).Svg(DottedLine);
                column.Item().AlignCenter().Element(DrawBox);
            });
        });
    }

    private static void DrawBox(IContainer box)
    {
        var width = (float)KdpMatterSpec.DrawBoxWidthPt;
        var height = (float)KdpMatterSpec.DrawBoxHeightPt;
        box.Width(width).Height(height).Layers(layers =>
        {
            layers.Layer().Svg(size => DashedBox(size, KdpMatterSpec.DrawBoxBorderPt, KdpMatterSpec.DrawBoxRadiusPt, KdpMatterSpec.MutedInk, dash: 6, gap: 4));
            layers.PrimaryLayer().PaddingBottom((float)KdpMatterSpec.DrawLabelBottomPt).AlignBottom().Element(label =>
                Centered(label, KdpFrontMatterWording.DrawLabel, KdpMatterSpec.DrawLabelPt, KdpMatterSpec.MutedInk, bold: false));
        });
    }

    public static void TheEnd(IContainer body, KdpPage page, KdpFrontMatter matter, KdpFrontPages front, bool rightToLeft)
    {
        Frame(body, page, rightToLeft).AlignMiddle().Element(item => Centered(item, matter.TheEnd, KdpMatterSpec.TheEndPt, front.EndColor, bold: true));
    }

    public static void Dino(IContainer body, KdpPage page, KdpFrontPages front, bool rightToLeft)
    {
        Frame(body, page, rightToLeft).PaddingHorizontal((float)KdpMatterSpec.DinoSidePaddingPt).AlignMiddle().Column(column =>
        {
            column.Spacing((float)KdpMatterSpec.DinoItemGapPt);
            column.Item().AlignCenter().Width((float)KdpMatterSpec.QrPt).Image(Cover.KdpQrCode.Png(KdpCoverWording.DinoReaderUrl)).UseOriginalImage().FitWidth();
            column.Item().Element(AddressBlock);
            column.Item().Element(item => Centered(item, KdpFrontMatterWording.ListenLine, KdpMatterSpec.ListenPt, KdpInteriorDocument.Ink, bold: false));
            column.Item().Element(JoinLine);
            if (front.Speakers.Count > 0)
            {
                column.Item().Element(item => Names(item, front.Speakers));
            }
        });
    }

    private static void AddressBlock(IContainer block)
    {
        block.Column(column =>
        {
            column.Spacing((float)KdpMatterSpec.AddressBlockGapPt);
            column.Item().Element(item => Centered(item, KdpFrontMatterWording.ScanLine, KdpMatterSpec.ScanPt, KdpMatterSpec.MutedInk, bold: false));
            column.Item().Text(t =>
            {
                t.AlignCenter();
                t.DefaultTextStyle(s => s.LineHeight((float)KdpMatterSpec.LineHeight).FontColor(KdpInteriorDocument.Ink));
                t.Span(KdpCoverWording.DinoShortAddress).FontSize((float)KdpMatterSpec.AddressPt).Bold().LetterSpacing((float)KdpMatterSpec.AddressLetterSpacingEm);
            });
            column.Item().Element(item => Centered(item, KdpFrontMatterWording.StoresLine, KdpMatterSpec.StoresPt, KdpMatterSpec.MutedInk, bold: false));
        });
    }

    private static void JoinLine(IContainer line)
    {
        line.Text(t =>
        {
            t.AlignCenter();
            t.DefaultTextStyle(s => s.FontSize((float)KdpMatterSpec.JoinPt).LineHeight((float)KdpMatterSpec.LineHeight).FontColor(KdpInteriorDocument.Ink));
            t.Span(KdpFrontMatterWording.JoinBefore);
            t.Span(KdpFrontMatterWording.JoinBold).Bold();
            t.Span(KdpFrontMatterWording.JoinAfter);
        });
    }

    /// <summary>The speaking characters, each in its colour, in one centred block that wraps between names (a name never breaks inside, its spaces are non-breaking).</summary>
    private static void Names(IContainer container, IReadOnlyList<KdpSpeakerName> speakers)
    {
        container.Text(t =>
        {
            t.AlignCenter();
            t.DefaultTextStyle(s => s.FontSize((float)KdpMatterSpec.NamesPt).LineHeight((float)KdpMatterSpec.LineHeight).Bold());
            for (var i = 0; i < speakers.Count; i++)
            {
                t.Span(speakers[i].Name.Replace(" ", NonBreakingSpace, StringComparison.Ordinal)).FontFamily(StoryFont.For(speakers[i].Name)).FontColor(speakers[i].Color);
                if (i + 1 < speakers.Count)
                {
                    t.Span(NameSeparator);
                }
            }
        });
    }

    private static void Centered(IContainer container, string text, double pt, string color, bool bold)
    {
        container.Text(t =>
        {
            t.AlignCenter();
            t.DefaultTextStyle(s => s.FontFamily(StoryFont.For(text)).FontSize((float)pt).FontColor(color).LineHeight((float)KdpMatterSpec.LineHeight));
            var span = t.Span(text);
            if (bold)
            {
                span.Bold();
            }
        });
    }

    /// <summary>The trim box of the page's own side, as a container of exactly that size; its content runs right to left in a right-to-left book (the direction is decided in Core, by the book's language).</summary>
    private static IContainer Frame(IContainer body, KdpPage page, bool rightToLeft)
    {
        var box = KdpInteriorDocument.InBox(body, KdpTrim.TrimRect(page.IsRightHand));
        return rightToLeft ? box.ContentFromRightToLeft() : box;
    }

    /// <summary>A dashed rounded rectangle whose stroke lies inside the box, as plain SVG (a vector in the PDF).</summary>
    private static string DashedBox(Size size, double stroke, double radius, string color, double dash, double gap)
    {
        var half = stroke / 2;
        var rect = $"<rect x=\"{N(half)}\" y=\"{N(half)}\" width=\"{N(size.Width - stroke)}\" height=\"{N(size.Height - stroke)}\" rx=\"{N(radius - half)}\" ry=\"{N(radius - half)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{N(stroke)}\" stroke-dasharray=\"{N(dash)} {N(gap)}\"/>";
        return Svg(size, rect);
    }

    /// <summary>A dotted line along the bottom of its box: round dots as wide as the stroke.</summary>
    private static string DottedLine(Size size)
    {
        var stroke = KdpMatterSpec.NameLineBorderPt;
        var y = size.Height - stroke / 2;
        var line = $"<line x1=\"{N(stroke / 2)}\" y1=\"{N(y)}\" x2=\"{N(size.Width - stroke / 2)}\" y2=\"{N(y)}\" stroke=\"{KdpInteriorDocument.Ink}\" stroke-width=\"{N(stroke)}\" stroke-linecap=\"round\" stroke-dasharray=\"0.01 {N(stroke * 2)}\"/>";
        return Svg(size, line);
    }

    private static string Svg(Size size, string shape)
    {
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(size.Width)}\" height=\"{N(size.Height)}\" viewBox=\"0 0 {N(size.Width)} {N(size.Height)}\">{shape}</svg>";
    }

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
