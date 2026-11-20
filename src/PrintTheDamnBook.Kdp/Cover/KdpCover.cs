using PrintTheDamnBook.Core.Dino;
using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Kdp.Cover;

/// <summary>What the cover needs: the title, the credits (null when the book has no readable back cover), the two encoded pictures (back null likewise) and the interior's page count.</summary>
public sealed record KdpCoverInput(string Title, string Language, DinoCredits? Credits, byte[] FrontPicture, byte[]? BackPicture, int PageCount, ReadingDirection Direction = ReadingDirection.LeftToRight, KdpBookSettings? Settings = null);

/// <summary>What was made: the PDF and the facts the check report lists.</summary>
public sealed record KdpCoverResult(
    byte[] Pdf,
    int PageCount,
    double WidthInches,
    double HeightInches,
    double SpineInches,
    Core.Layout.RectD Barcode,
    bool BackPictureRead,
    bool CreditsRead,
    ReadingDirection Direction,
    KdpFontCheck Fonts,
    IReadOnlyList<string> Warnings,
    KdpPicture? FrontPrepared = null,
    KdpPicture? BackPrepared = null);

/// <summary>Makes the KDP cover PDF (Slice 29): sizes from Core <see cref="KdpCoverSize"/>, pictures through the same decoder as the interior, drawing in <see cref="KdpCoverDocument"/>.</summary>
public static class KdpCover
{
    public const string NoBackCoverWarning = "back cover: no Dino back cover read (picture and credits missing; Slice 34)";
    public const string NoCreditsWarning = "back cover: the book has a back cover picture but no readable credits; the credit lines are left out";
    public const string FrontCutLineWarning = "cover: the front picture may be cut through a character at the trim edge; look at the cover before uploading";

    public const string BackCutLineWarning = "cover: the back picture may be cut through a character at the trim edge; look at the cover before uploading";

    public static KdpCoverResult Build(KdpCoverInput input)
    {
        var (drawing, front, back, warnings) = Prepare(input);
        var pdf = KdpCoverDocument.Render(drawing);
        var fonts = KdpPdfInspector.CheckFonts(pdf);
        if (!fonts.AllEmbedded)
        {
            throw new KdpException($"{fonts.Fonts - fonts.Embedded} of {fonts.Fonts} fonts in the cover PDF are not embedded; KDP rejects that.");
        }

        return new KdpCoverResult(pdf, input.PageCount, KdpCoverSize.CoverWidth(input.PageCount), KdpCoverSize.CoverHeight, KdpCoverSize.SpineWidth(input.PageCount), KdpCoverSize.Barcode(input.PageCount, input.Direction), back is not null, input.Credits is not null, input.Direction, fonts, warnings, front, back);
    }

    /// <summary>The cover's drawing, its prepared pictures and the warnings found while preparing them; shared by the PDF (<see cref="Build"/>) and the small front tile of the last page's "More books" (Slice 41).</summary>
    internal static (KdpCoverDrawing Drawing, KdpPicture Front, KdpPicture? Back, List<string> Warnings) Prepare(KdpCoverInput input)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        KdpFonts.EnsureRegistered();
        var warnings = new List<string>();
        var settings = input.Settings ?? KdpBookSettings.Empty;
        var front = KdpImageDecoder.Prepare(input.FrontPicture, settings.CoverOffsetFor(KdpPictureKeys.Front), settings.HasCoverOffset(KdpPictureKeys.Front));
        var back = input.BackPicture is { } bytes ? KdpImageDecoder.Prepare(bytes, settings.CoverOffsetFor(KdpPictureKeys.Back), settings.HasCoverOffset(KdpPictureKeys.Back)) : null;
        if (input.BackPicture is null)
        {
            warnings.Add(NoBackCoverWarning);
        }

        if (input.Credits is null && input.BackPicture is not null)
        {
            warnings.Add(NoCreditsWarning);
        }

        if (front.Flagged)
        {
            warnings.Add(FrontCutLineWarning);
        }

        if (back is { Flagged: true })
        {
            warnings.Add(BackCutLineWarning);
        }

        warnings.AddRange(new[] { KdpInterior.OffsetWarning("Front cover", front), back is null ? null : KdpInterior.OffsetWarning("Back cover", back) }.OfType<string>());
        settings = WithAppliedOffsets(settings, front, back);
        AddStaleTextWarnings(settings, back is not null, warnings);
        var drawing = new KdpCoverDrawing(
            input.Title,
            input.Language,
            input.Credits,
            front,
            back,
            input.PageCount,
            input.Direction,
            settings.UsableCoverText(KdpPictureKeys.Front),
            back is null ? null : settings.UsableCoverText(KdpPictureKeys.Back),
            settings.DinoColors,
            warnings,
            back is null ? null : settings.UsableDinoText?.Box);
        return (drawing, front, back, warnings);
    }

    /// <summary>The settings with each chosen cover offset replaced by the one drawn (an over-max request is cut back), so a text choice is judged stale against the crop really in use.</summary>
    private static KdpBookSettings WithAppliedOffsets(KdpBookSettings settings, KdpPicture front, KdpPicture? back)
    {
        settings = settings.HasCoverOffset(KdpPictureKeys.Front) ? settings.WithCoverOffset(KdpPictureKeys.Front, front.AppliedOffsetPx) : settings;
        return back is not null && settings.HasCoverOffset(KdpPictureKeys.Back) ? settings.WithCoverOffset(KdpPictureKeys.Back, back.AppliedOffsetPx) : settings;
    }

    /// <summary>A saved text choice made for another crop offset is not used (the fallback placement is); each such cover gets a warning that its words must be decided again.</summary>
    private static void AddStaleTextWarnings(KdpBookSettings settings, bool hasBack, List<string> warnings)
    {
        foreach (var key in hasBack ? new[] { KdpPictureKeys.Front, KdpPictureKeys.Back } : [KdpPictureKeys.Front])
        {
            if (settings.CoverTextStale(key))
            {
                warnings.Add($"cover: the text choice saved for the {key} cover was made for another crop of its picture, so the fallback placement is used; decide the {key} words again with --text.");
            }
        }

        if (hasBack && settings.DinoBoxStale)
        {
            warnings.Add("cover: the Dino block box saved for the back cover was made for another crop of its picture, so the QR code and line stay in the bottom corner; decide it again with --dino-text.");
        }
    }
}
