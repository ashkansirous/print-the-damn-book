using System.Globalization;
using System.Text;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Kdp.Pictures;

namespace PrintTheDamnBook.Kdp;

/// <summary>The <c>check-report.md</c> written beside <c>interior.pdf</c>: what the PDF holds and every warning, so the owner (and later slices) can see it without opening the PDF.</summary>
public static class KdpCheckReport
{
    public static string Build(string title, string language, KdpInteriorResult result)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"# KDP interior check: {title} ({language})");
        text.AppendLine();
        AppendSummary(text, result);
        AppendPages(text, result);
        AppendPictures(text, result);
        AppendWarnings(text, result);
        return text.ToString();
    }

    private static void AppendSummary(StringBuilder text, KdpInteriorResult result)
    {
        var fonts = result.Fonts;
        text.AppendLine(CultureInfo.InvariantCulture, $"- Pages: {result.Plan.PageCount} (even: {result.Plan.PageCount % 2 == 0}, minimum {KdpPagePlan.MinPages})");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Page size: {KdpTrim.PageWidthInches} x {KdpTrim.PageHeightInches} in on every page (trim {KdpTrim.TrimInches} x {KdpTrim.TrimInches} in plus bleed)");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Fonts embedded: {fonts.Embedded} of {fonts.Fonts} ({string.Join(", ", fonts.Names)})");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Text pages facing their pictures: {Facing(result.Plan)}");
        text.AppendLine();
    }

    /// <summary>"yes" when every text page and its picture are the two pages of one spread.</summary>
    private static string Facing(KdpPlan plan)
    {
        var pictures = plan.Pages.Where(p => p.Role == KdpPageRole.StoryPicture).ToDictionary(p => p.StoryIndex!.Value);
        var ok = plan.Pages.Where(p => p.Role == KdpPageRole.StoryText).All(t =>
            pictures[t.StoryIndex!.Value] is var p && p.IsRightHand != t.IsRightHand && Math.Abs(p.Position - t.Position) == 1 && Math.Min(p.Position, t.Position) % 2 == 0);
        return ok ? "yes, every one" : "NO";
    }

    private static void AppendPages(StringBuilder text, KdpInteriorResult result)
    {
        text.AppendLine("## Pages");
        text.AppendLine();
        text.AppendLine("| Page | Side | Role | Story page | Number | Text size |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var page in result.Plan.Pages)
        {
            var size = result.Stories.FirstOrDefault(s => page.Role == KdpPageRole.StoryText && s.StoryIndex == page.StoryIndex)?.Pt;
            text.AppendLine(CultureInfo.InvariantCulture, $"| {page.Position} | {(page.IsRightHand ? "right" : "left")} | {page.Role} | {page.StoryIndex + 1} | {page.Number} | {(size is { } pt ? $"{pt} pt" : string.Empty)} |");
        }

        text.AppendLine();
    }

    private static void AppendPictures(StringBuilder text, KdpInteriorResult result)
    {
        text.AppendLine("## Pictures");
        text.AppendLine();
        text.AppendLine("| Story page | Source px | Kept (x, y, w x h) | Target px | Scale | Effective dpi | Cut line | Preview |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        for (var i = 0; i < result.Pictures.Count; i++)
        {
            var p = result.Pictures[i];
            var c = p.Crop.Source;
            text.AppendLine(CultureInfo.InvariantCulture, $"| {i + 1} | {p.SourceWidth} x {p.SourceHeight} | {c.X}, {c.Y}, {c.Width} x {c.Height} | {p.Crop.TargetWidth} x {p.Crop.TargetHeight} | {p.Crop.Scale:0.00}x | {p.Crop.EffectiveDpi:0} | {CutLineText(p)} | {(p.Preview is null ? string.Empty : CutLineInputs.PreviewPath(i + 1))} |");
        }

        text.AppendLine();
    }

    /// <summary>"FLAGGED" with the score, "chosen" for a page the owner set an offset for, otherwise "ok".</summary>
    private static string CutLineText(KdpPicture picture)
    {
        if (picture.OffsetIgnored)
        {
            return $"offset {picture.RequestedOffsetPx} px ignored (no spare height)";
        }

        if (picture.OffsetClamped)
        {
            return $"chosen offset {picture.AppliedOffsetPx} px (asked {picture.RequestedOffsetPx}, clamped)";
        }

        if (picture.OffsetChosen)
        {
            return $"chosen offset {picture.AppliedOffsetPx} px";
        }

        return picture.Cut.Flagged ? string.Create(CultureInfo.InvariantCulture, $"FLAGGED (score {picture.Cut.Score:0.0})") : string.Create(CultureInfo.InvariantCulture, $"ok (score {picture.Cut.Score:0.0})");
    }

    private static void AppendWarnings(StringBuilder text, KdpInteriorResult result)
    {
        text.AppendLine("## Warnings");
        text.AppendLine();
        if (result.Warnings.Count == 0)
        {
            text.AppendLine("None.");
        }

        foreach (var warning in result.Warnings)
        {
            text.AppendLine($"- {warning}");
        }
    }
}
