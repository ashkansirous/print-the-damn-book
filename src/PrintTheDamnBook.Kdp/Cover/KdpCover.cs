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
        var drawing = new KdpCoverDrawing(input.Title, input.Language, input.Credits, front, back, input.PageCount, input.Direction);
        var pdf = KdpCoverDocument.Render(drawing);
        var fonts = KdpPdfInspector.CheckFonts(pdf);
        if (!fonts.AllEmbedded)
        {
            throw new KdpException($"{fonts.Fonts - fonts.Embedded} of {fonts.Fonts} fonts in the cover PDF are not embedded; KDP rejects that.");
        }

        return new KdpCoverResult(pdf, input.PageCount, KdpCoverSize.CoverWidth(input.PageCount), KdpCoverSize.CoverHeight, KdpCoverSize.SpineWidth(input.PageCount), KdpCoverSize.Barcode(input.PageCount, input.Direction), back is not null, input.Credits is not null, input.Direction, fonts, warnings, front, back);
    }
}
