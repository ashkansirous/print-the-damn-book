using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;
using Polygon = System.Windows.Shapes.Polygon;

namespace PrintTheDamnBook.App.Printing;

/// <summary>Draws sheet faces into a <see cref="FixedDocument"/>, one landscape page per face. Placement comes from Core.</summary>
internal sealed class FaceRenderer(PaperKind paper, double marginMm)
{
    private const double PrintDpi = 300;
    private const double FeedArrowMm = 7;
    private const double FeedNoteTextMm = 55;
    private const double FeedNoteInsetMm = 8;
    private const double FeedNoteGapMm = 2;
    private const double FeedNotePaddingMm = 1.5;
    private const double FeedNoteBorderDip = 1;

    /// <summary>Printed on the test sheet's front; the reload dialog repeats it.</summary>
    public const string FeedEdgeInstruction = "FRONT – put the stack back printed side UP with this arrow pointing INTO the printer";

    private readonly SizeD _sheetMm = Paper.LandscapeSheetMm(paper);

    /// <summary>Renders each sheet as its front then its back, unrotated.</summary>
    public FixedDocument Render(IEnumerable<Sheet> sheets)
    {
        return Render(sheets.SelectMany(s => new[] { new PlannedFace(s.Front, false, false), new PlannedFace(s.Back, false, false) }).ToList());
    }

    /// <summary>Renders the faces in the given order, one page each, turned and marked as planned.</summary>
    public FixedDocument Render(IReadOnlyList<PlannedFace> faces)
    {
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(ToDip(_sheetMm.Width), ToDip(_sheetMm.Height));
        var sizes = ReadAllSizes(faces.Select(f => f.Face));
        foreach (var face in faces)
        {
            document.Pages.Add(ToPageContent(face, sizes));
        }

        return document;
    }

    /// <summary>Checks that every image of the faces can be read, so a problem is found before anything is sent.</summary>
    public static void CheckReadable(IEnumerable<PlannedFace> faces)
    {
        ReadAllSizes(faces.Select(f => f.Face));
    }

    /// <summary>Reads every image header up front so an unreadable file fails before any pixels are decoded.</summary>
    private static Dictionary<string, SizeD> ReadAllSizes(IEnumerable<SheetFace> faces)
    {
        var sizes = new Dictionary<string, SizeD>();
        var paths = faces.SelectMany(f => new[] { f.Left, f.Right })
            .Select(p => p.Path)
            .OfType<string>()
            .Distinct();
        foreach (var path in paths)
        {
            sizes[path] = ImageDecoder.ReadPixelSize(path) ?? throw Unreadable(path);
        }

        return sizes;
    }

    private PageContent ToPageContent(PlannedFace planned, IReadOnlyDictionary<string, SizeD> sizes)
    {
        var page = new FixedPage { Width = ToDip(_sheetMm.Width), Height = ToDip(_sheetMm.Height), Background = Brushes.White };
        AddPage(page, planned.Face.Left, false, planned.Rotate180, sizes);
        AddPage(page, planned.Face.Right, true, planned.Rotate180, sizes);
        if (planned.MarksFeedEdge)
        {
            page.Children.Add(FeedEdgeNote());
        }

        page.Measure(new Size(page.Width, page.Height));
        page.Arrange(new Rect(0, 0, page.Width, page.Height));

        var content = new PageContent();
        ((IAddChild)content).AddChild(page);
        return content;
    }

    /// <summary>
    /// A transparent, outlined arrow and instruction on the test sheet's front, centred on the fold line near the top and
    /// well inside the printable area. It has no fill so it does not hide the page content. The arrow points at the
    /// image's left edge; the instruction tells the user to feed that edge into the printer for the backs.
    /// </summary>
    private Border FeedEdgeNote()
    {
        var arrowSize = ToDip(FeedArrowMm);
        var arrow = new Polygon
        {
            Points = [new Point(arrowSize, 0), new Point(arrowSize, arrowSize), new Point(0, arrowSize / 2)],
            Fill = Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, ToDip(FeedNoteGapMm), 0),
        };
        var text = new TextBlock
        {
            Text = FeedEdgeInstruction,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            Width = ToDip(FeedNoteTextMm),
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(arrow);
        row.Children.Add(text);
        var widthMm = FeedArrowMm + FeedNoteGapMm + FeedNoteTextMm + 2 * FeedNotePaddingMm;
        var box = new Border
        {
            Child = row,
            Width = ToDip(widthMm) + 2 * FeedNoteBorderDip,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(FeedNoteBorderDip),
            Padding = new Thickness(ToDip(FeedNotePaddingMm)),
        };
        FixedPage.SetLeft(box, ToDip(_sheetMm.Width / 2 - widthMm / 2));
        FixedPage.SetTop(box, ToDip(FeedNoteInsetMm));
        return box;
    }

    private void AddPage(FixedPage page, BookPage bookPage, bool rightHalf, bool rotate180, IReadOnlyDictionary<string, SizeD> sizes)
    {
        if (bookPage.Path is null)
        {
            return;
        }

        var size = sizes[bookPage.Path];
        var rect = HalfSheetLayout.Place(size, _sheetMm, rightHalf, marginMm);
        if (rotate180)
        {
            rect = HalfSheetLayout.Rotate180(rect, _sheetMm);
        }

        var image = ImageDecoder.Decode(bookPage.Path, TargetPixelWidth(rect.Width, size.Width)) ?? throw Unreadable(bookPage.Path);
        var element = new Image
        {
            Source = image,
            Stretch = Stretch.Fill,
            Width = ToDip(rect.Width),
            Height = ToDip(rect.Height),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = rotate180 ? new RotateTransform(180) : Transform.Identity,
        };
        FixedPage.SetLeft(element, ToDip(rect.X));
        FixedPage.SetTop(element, ToDip(rect.Y));
        page.Children.Add(element);
    }

    /// <summary>Pixels needed to print the width at <see cref="PrintDpi"/>, or 0 (decode as is) when the image is not larger.</summary>
    private static int TargetPixelWidth(double printedWidthMm, double sourcePixelWidth)
    {
        var target = Math.Ceiling(printedWidthMm / Paper.MillimetresPerInch * PrintDpi);
        return target < sourcePixelWidth ? (int)target : 0;
    }

    private static PrintFailedException Unreadable(string path)
    {
        return new PrintFailedException($"Could not read the image {Path.GetFileName(path)}.");
    }

    private static double ToDip(double millimetres)
    {
        return millimetres * Paper.DipsPerMillimetre;
    }
}
