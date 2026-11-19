using System.Globalization;
using System.Text;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;
using PrintTheDamnBook.Kdp.Cover;
using PrintTheDamnBook.Kdp.Pictures;

namespace PrintTheDamnBook.Kdp;

/// <summary>What happened to a <c>cover.pdf</c> from an earlier run when this run made no cover (it is renamed, never deleted).</summary>
public enum StaleCover
{
    NotApplicable,
    None,
    Renamed,
    RenameFailed,
}

/// <summary>The <c>check-report.md</c> written beside <c>interior.pdf</c>: what the PDF holds and every warning, so the owner (and later slices) can see it without opening the PDF.</summary>
public static class KdpCheckReport
{
    public static string Build(string title, string language, KdpInteriorResult result, KdpCoverResult? cover = null, string? coverError = null, StaleCover stale = StaleCover.NotApplicable)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"# KDP interior check: {title} ({language})");
        text.AppendLine();
        AppendSummary(text, result);
        if (cover is not null)
        {
            AppendCover(text, cover);
        }
        else if (coverError is not null)
        {
            AppendCoverFailed(text, coverError, stale);
        }

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

    /// <summary>The cover.pdf facts: size from KDP's formula, the spine and the clear barcode box (inches from the top-left of the file).</summary>
    private static void AppendCover(StringBuilder text, KdpCoverResult cover)
    {
        var box = cover.Barcode;
        text.AppendLine("## Cover");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"- Cover size: {cover.WidthInches:0.0000} x {cover.HeightInches:0.0000} in (back + spine + front, bleed {KdpCoverSize.BleedInches} in on the top, bottom and outside edges)");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Spine width: {cover.SpineInches:0.0000} in for {cover.PageCount} pages ({KdpCoverSize.SpineInchesPerPage} in per page, premium colour); {SpineTextNote(cover.PageCount)}");
        var corner = cover.Direction == ReadingDirection.RightToLeft ? "lower left" : "lower right";
        text.AppendLine(CultureInfo.InvariantCulture, $"- Barcode box, left clear: {box.Width} x {box.Height} in at x {box.X:0.000}, y {box.Y:0.000} in ({corner} of the back cover)");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Back cover picture read from the book: {(cover.BackPictureRead ? "yes" : "no")}; credit lines read: {(cover.CreditsRead ? "yes" : "no")}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Cover fonts embedded: {cover.Fonts.Embedded} of {cover.Fonts.Fonts}");
        text.AppendLine();
    }

    private static string SpineTextNote(int pageCount) => KdpCoverSize.HasSpineText(pageCount)
        ? $"no spine text is drawn (KDP allows it from {KdpCoverSize.SpineTextMinPages} pages; this book has {pageCount})"
        : $"no spine text (KDP needs at least {KdpCoverSize.SpineTextMinPages} pages)";

    private static void AppendCoverFailed(StringBuilder text, string coverError, StaleCover stale)
    {
        text.AppendLine("## Cover");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"- cover.pdf was not written: {coverError}{StaleNote(stale)}");
        text.AppendLine();
    }

    private static string StaleNote(StaleCover stale) => StaleText(stale) is { } text ? $" ({text})" : string.Empty;

    /// <summary>The one line about an older cover.pdf, shared by the report and the command line; null when there is nothing to say.</summary>
    public static string? StaleText(StaleCover stale) => stale switch
    {
        StaleCover.Renamed => $"older {KdpExporter.CoverName} kept as {KdpExporter.PreviousCoverName}",
        StaleCover.RenameFailed => $"could not rename the older {KdpExporter.CoverName} to {KdpExporter.PreviousCoverName}, delete it before upload: it does NOT match this interior",
        _ => null,
    };

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
