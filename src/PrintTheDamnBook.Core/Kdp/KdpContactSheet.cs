namespace PrintTheDamnBook.Core.Kdp;

/// <summary>A rectangle in pixels, from the top-left of the contact sheet.</summary>
public readonly record struct PixelBox(int X, int Y, int Width, int Height);

/// <summary>
/// The grid of the contact sheet (Slice 30): the interior's pages in spread order (page 1 alone on the right of the first spread, then
/// left-right pairs), small enough to read in one picture. Page size and raster maths live here; the Kdp project only draws.
/// </summary>
public sealed record KdpContactSheetLayout(int PageCount, int Columns, int Rows, int CellWidth, int CellHeight, int Gap, int LabelHeight)
{
    public const int PageRasterDpi = 48;

    public const int MaxSheetWidthPx = 1600;

    public const int PagePreviewWidthPx = 200;

    public const int GapPx = 8;

    public const int LabelHeightPx = 16;

    public int SheetWidth => Columns * CellWidth + (Columns + 1) * Gap;

    public int SheetHeight => Rows * (CellHeight + LabelHeight) + (Rows + 1) * Gap;

    /// <summary>The picture box of a page (position from 1), below its label. Page 1 is a right-hand page, so slot 0 (left of it) stays empty.</summary>
    public PixelBox PageBox(int position)
    {
        var slot = Slot(position);
        var column = slot % Columns;
        var row = slot / Columns;
        return new PixelBox(Gap + column * (CellWidth + Gap), Gap + row * (CellHeight + LabelHeight + Gap) + LabelHeight, CellWidth, CellHeight);
    }

    /// <summary>The label strip above a page's box.</summary>
    public PixelBox LabelBox(int position)
    {
        var box = PageBox(position);
        return new PixelBox(box.X, box.Y - LabelHeight, box.Width, LabelHeight);
    }

    private int Slot(int position)
    {
        if (position < 1 || position > PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, $"A page from 1 to {PageCount} is needed.");
        }

        return position;
    }

    /// <summary>
    /// Fits <paramref name="pageCount"/> pages of the KDP page size into rows of whole spreads no wider than <see cref="MaxSheetWidthPx"/>;
    /// a cell is <see cref="PagePreviewWidthPx"/> wide and as tall as the page's shape asks.
    /// </summary>
    public static KdpContactSheetLayout For(int pageCount)
    {
        if (pageCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageCount), pageCount, "At least one page is needed.");
        }

        var spreadWidth = 2 * PagePreviewWidthPx + 2 * GapPx;
        var spreadsPerRow = Math.Max(1, (MaxSheetWidthPx - GapPx) / spreadWidth);
        var columns = 2 * spreadsPerRow;
        var slots = pageCount + 1;
        var rows = (slots + columns - 1) / columns;
        var height = (int)Math.Round(PagePreviewWidthPx * KdpTrim.PageHeightInches / KdpTrim.PageWidthInches);
        return new KdpContactSheetLayout(pageCount, columns, rows, PagePreviewWidthPx, height, GapPx, LabelHeightPx);
    }
}
