using System.Globalization;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>A whole, uncut picture shrunk to fit a box, and where the kept window lies in it (all in pixels of the shrunk picture).</summary>
public sealed record KdpCropOutline(int ImageWidth, int ImageHeight, PixelRect Window, int AppliedOffsetPx, int MaxOffsetPx)
{
    /// <summary>
    /// The outline of the part <see cref="KdpPictureCrop"/> keeps of a <paramref name="sourceWidth"/> x <paramref name="sourceHeight"/> picture at
    /// <paramref name="offsetPx"/>, with the picture scaled down (never up) to fit <paramref name="maxWidth"/> x <paramref name="maxHeight"/>.
    /// </summary>
    public static KdpCropOutline For(int sourceWidth, int sourceHeight, int offsetPx, int maxWidth, int maxHeight)
    {
        var crop = KdpPictureCrop.For(sourceWidth, sourceHeight, offsetPx);
        var scale = Math.Min(1.0, Math.Min((double)maxWidth / sourceWidth, (double)maxHeight / sourceHeight));
        var width = Math.Max(1, Scaled(sourceWidth, scale));
        var height = Math.Max(1, Scaled(sourceHeight, scale));
        var left = Scaled(crop.Source.X, scale);
        var top = Scaled(crop.Source.Y, scale);
        var right = Math.Min(width, Scaled(crop.Source.X + crop.Source.Width, scale));
        var bottom = Math.Min(height, Scaled(crop.Source.Y + crop.Source.Height, scale));
        return new KdpCropOutline(width, height, new PixelRect(left, top, right - left, bottom - top), crop.Source.Y, KdpPictureCrop.MaxOffset(sourceWidth, sourceHeight));
    }

    private static int Scaled(int value, double scale) => (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);
}

/// <summary>
/// The grid of the crop sheet (Slice 36, <c>--crop-sheet</c>): every picture of a book, whole and uncut, about <see cref="CellWidthPx"/> wide, in
/// reading order (story pages, then front, then back), small enough that the crop agent reads a whole book in one picture. Maths here, drawing in the Kdp project.
/// </summary>
public sealed record KdpCropSheetLayout(int Count, int Columns, int Rows)
{
    public const string FolderName = "crop";
    public const string SheetName = "sheet.png";

    public const int CellWidthPx = 200;

    /// <summary>A Dino picture is 3:4, the tallest the sheet expects; a taller one is shrunk to fit.</summary>
    public const int CellHeightPx = 267;

    public const int GapPx = 8;

    public const int LabelHeightPx = 16;

    /// <summary>The width of the larger uncut copy the agent opens when it is unsure about a picture.</summary>
    public const int DetailWidthPx = 480;

    public const int DetailMaxHeightPx = 720;

    /// <summary>The strip above a larger copy that carries the same label as the sheet, so the agent never assumes the maximum offset.</summary>
    public const int DetailLabelHeightPx = 20;

    private const int ColumnCount = 6;

    public int SheetWidth => Columns * CellWidthPx + (Columns + 1) * GapPx;

    public int SheetHeight => Rows * (CellHeightPx + LabelHeightPx) + (Rows + 1) * GapPx;

    /// <summary>The box of a picture (index from 0), below its label.</summary>
    public PixelBox CellBox(int index)
    {
        var (column, row) = Place(index);
        return new PixelBox(GapPx + column * (CellWidthPx + GapPx), GapPx + row * (CellHeightPx + LabelHeightPx + GapPx) + LabelHeightPx, CellWidthPx, CellHeightPx);
    }

    public PixelBox LabelBox(int index)
    {
        var box = CellBox(index);
        return new PixelBox(box.X, box.Y - LabelHeightPx, box.Width, LabelHeightPx);
    }

    /// <summary>The text above a picture: its key and the offset in use out of the most the picture allows, so the agent can answer in pixels.</summary>
    public static string Label(string key, int appliedOffsetPx, int maxOffsetPx)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{key} y={appliedOffsetPx}/{maxOffsetPx}");
    }

    /// <summary>The file of a picture's larger copy, relative to the output folder.</summary>
    public static string DetailPath(string key) => string.Create(CultureInfo.InvariantCulture, $"{FolderName}/{key}.png");

    public static KdpCropSheetLayout For(int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "At least one picture is needed.");
        }

        return new KdpCropSheetLayout(count, Math.Min(count, ColumnCount), (count + ColumnCount - 1) / ColumnCount);
    }

    private (int Column, int Row) Place(int index)
    {
        if (index < 0 || index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"A picture from 0 to {Count - 1} is needed.");
        }

        return (index % Columns, index / Columns);
    }
}
