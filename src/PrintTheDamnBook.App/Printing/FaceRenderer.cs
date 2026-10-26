using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;
using PrintTheDamnBook.Core.Text;
using Polygon = System.Windows.Shapes.Polygon;

namespace PrintTheDamnBook.App.Printing;

/// <summary>Draws sheet faces into a <see cref="FixedDocument"/>, one landscape page per face. Placement comes from Core.</summary>
internal sealed class FaceRenderer(PaperKind paper, JobLayout layout)
{
    private const double PrintDpi = 300;
    private const double FeedArrowMm = 7;
    private const double FeedNoteTextMm = 55;
    private const double FeedNoteInsetMm = 8;
    private const double FeedNoteGapMm = 2;
    private const double FeedNotePaddingMm = 1.5;
    private const double FeedNoteBorderDip = 1;
    private const string NumberFontFamily = "Segoe UI";
    private const double NumberPixelsPerDip = 1.0;
    private static readonly Brush PageNumberBrush = FrozenBrush(Color.FromRgb(0x44, 0x44, 0x44));
    private static readonly Typeface NumberTypeface = new(NumberFontFamily);

    /// <summary>Printed on the test sheet's front; the reload dialog repeats it.</summary>
    public const string FeedEdgeInstruction = "FRONT – put the stack back printed side UP with this arrow pointing INTO the printer";

    private readonly SizeD _sheetMm = Paper.LandscapeSheetMm(paper);

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
        if (bookPage.Kind == PageKind.Text)
        {
            AddTextPage(page, bookPage, rightHalf, rotate180);
            return;
        }

        if (bookPage.Path is null)
        {
            return;
        }

        var size = sizes[bookPage.Path];
        // Every page reserves the same number strip (even unnumbered covers and blanks) so all pages print the same size and facing pages match.
        var placement = HalfSheetLayout.Resolve(size, _sheetMm, rightHalf, layout.MarginMm, layout.Scaling, layout.NumberPosition);
        var rect = rotate180 ? placement.Rotate180(_sheetMm).Image : placement.Image;

        var image = ImageDecoder.Decode(bookPage.Path, TargetPixelWidth(rect.Width, size.Width)) ?? throw Unreadable(bookPage.Path);
        var element = new Image
        {
            Source = image,
            Stretch = Stretch.Fill,
            Width = ToDip(rect.Width),
            Height = ToDip(rect.Height),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = rotate180 ? new RotateTransform(180) : Transform.Identity,
            Clip = layout.Scaling == ScalingMode.Fill ? ClipOf(placement) : null,
        };
        FixedPage.SetLeft(element, ToDip(rect.X));
        FixedPage.SetTop(element, ToDip(rect.Y));
        page.Children.Add(element);
        AddBand(page, bookPage, placement, rotate180);
        AddPageNumber(page, bookPage, rightHalf, rotate180);
    }

    /// <summary>A page with words on the picture: the soft panel over the bottom of the visible picture, then the words. Both turn with the face.</summary>
    private void AddBand(FixedPage page, BookPage bookPage, PagePlacement placement, bool rotate180)
    {
        if (!bookPage.HasBandText)
        {
            return;
        }

        var painting = StoryPainter.PaintBand(bookPage.Story!.Text, paper, placement.Visible);
        var panel = rotate180 ? RotateDip(painting.Panel) : painting.Panel;
        var box = new Border
        {
            Width = panel.Width,
            Height = panel.Height,
            Background = StoryPainter.Panel,
            CornerRadius = new CornerRadius(painting.CornerRadius),
        };
        FixedPage.SetLeft(box, panel.X);
        FixedPage.SetTop(box, panel.Y);
        page.Children.Add(box);
        AddWords(page, painting.Words, painting.WordsClip, rotate180);
    }

    /// <summary>A story text page: the paper tint when the book asks for one (the whole half sheet), then the words centred in their box.</summary>
    private void AddTextPage(FixedPage page, BookPage bookPage, bool rightHalf, bool rotate180)
    {
        var area = StoryTextLayout.TextPage(_sheetMm, rightHalf, layout.MarginMm, layout.NumberPosition);
        if (layout.TextPageTint is { } tint)
        {
            AddTint(page, rotate180 ? HalfSheetLayout.Rotate180(area.Page, _sheetMm) : area.Page, tint);
        }

        if (bookPage.Story is { IsEmpty: false } story)
        {
            var painting = StoryPainter.PaintTextPage(story.Text, paper, area);
            AddWords(page, painting.Words, painting.WordsClip, rotate180);
        }

        AddPageNumber(page, bookPage, rightHalf, rotate180);
    }

    private void AddTint(FixedPage page, RectD half, Rgb tint)
    {
        var brush = FrozenBrush(Color.FromRgb(tint.R, tint.G, tint.B));
        var fill = new System.Windows.Shapes.Rectangle { Width = ToDip(half.Width), Height = ToDip(half.Height), Fill = brush };
        FixedPage.SetLeft(fill, ToDip(half.X));
        FixedPage.SetTop(fill, ToDip(half.Y));
        page.Children.Add(fill);
    }

    /// <summary>Words as outlines already in sheet coordinates; a turned face turns them about the sheet's centre.</summary>
    private void AddWords(FixedPage page, Geometry words, Rect clip, bool rotate180)
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = words,
            Fill = StoryPainter.Ink,
            Clip = new RectangleGeometry(clip),
            RenderTransform = rotate180 ? new RotateTransform(180, ToDip(_sheetMm.Width) / 2, ToDip(_sheetMm.Height) / 2) : Transform.Identity,
        };
        page.Children.Add(path);
    }

    private Rect RotateDip(Rect rect)
    {
        var width = ToDip(_sheetMm.Width);
        var height = ToDip(_sheetMm.Height);
        return new Rect(width - rect.X - rect.Width, height - rect.Y - rect.Height, rect.Width, rect.Height);
    }

    /// <summary>Draws the page's number in its strip below or above the image; it turns with the face.</summary>
    private void AddPageNumber(FixedPage page, BookPage bookPage, bool rightHalf, bool rotate180)
    {
        var strip = HalfSheetLayout.PageNumberRect(_sheetMm, rightHalf, layout.MarginMm, layout.NumberPosition);
        if (strip is null || layout.PageNumbers is null || !layout.PageNumbers.Numbers.TryGetValue(bookPage, out var number))
        {
            return;
        }

        var rect = rotate180 ? HalfSheetLayout.Rotate180(strip.Value, _sheetMm) : strip.Value;
        var path = new System.Windows.Shapes.Path
        {
            Data = NumberGeometry(number, ToDip(PageNumbering.FontMm(_sheetMm))),
            Fill = PageNumberBrush,
        };
        var bounds = path.Data.Bounds;
        var centre = new Point(ToDip(rect.X + rect.Width / 2), ToDip(rect.Y + rect.Height / 2));
        var move = new TranslateTransform(centre.X - (bounds.X + bounds.Width / 2), centre.Y - (bounds.Y + bounds.Height / 2));
        path.RenderTransform = rotate180 ? new TransformGroup { Children = { move, new RotateTransform(180, centre.X, centre.Y) } } : move;
        page.Children.Add(path);
    }

    /// <summary>The digits as outlines, so the caller can centre them by their real ink bounds rather than a line-height guess.</summary>
    private static Geometry NumberGeometry(int number, double fontDip)
    {
        var text = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, NumberTypeface, fontDip, PageNumberBrush, NumberPixelsPerDip);
        return text.BuildGeometry(new Point(0, 0));
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>The visible part in the image's own coordinates; it turns with the image, so it is the same for a rotated page.</summary>
    private static RectangleGeometry ClipOf(PagePlacement placement)
    {
        var clip = placement.LocalClip;
        return new RectangleGeometry(new Rect(ToDip(clip.X), ToDip(clip.Y), ToDip(clip.Width), ToDip(clip.Height)));
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
