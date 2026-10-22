using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Printing;

/// <summary>Draws sheet faces into a <see cref="FixedDocument"/>, one landscape page per face. Placement comes from Core.</summary>
internal sealed class FaceRenderer(PaperKind paper, double marginMm)
{
    private const double PrintDpi = 300;

    private readonly SizeD _sheetMm = Paper.LandscapeSheetMm(paper);

    public FixedDocument Render(IEnumerable<Sheet> sheets)
    {
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(ToDip(_sheetMm.Width), ToDip(_sheetMm.Height));
        var sizes = ReadAllSizes(sheets);
        foreach (var sheet in sheets)
        {
            document.Pages.Add(ToPageContent(sheet.Front, sizes));
            document.Pages.Add(ToPageContent(sheet.Back, sizes));
        }

        return document;
    }

    /// <summary>Reads every image header up front so an unreadable file fails before any pixels are decoded.</summary>
    private static Dictionary<string, SizeD> ReadAllSizes(IEnumerable<Sheet> sheets)
    {
        var sizes = new Dictionary<string, SizeD>();
        var paths = sheets.SelectMany(s => new[] { s.Front.Left, s.Front.Right, s.Back.Left, s.Back.Right })
            .Select(p => p.Path)
            .OfType<string>()
            .Distinct();
        foreach (var path in paths)
        {
            sizes[path] = ImageDecoder.ReadPixelSize(path) ?? throw Unreadable(path);
        }

        return sizes;
    }

    private PageContent ToPageContent(SheetFace face, IReadOnlyDictionary<string, SizeD> sizes)
    {
        var page = new FixedPage { Width = ToDip(_sheetMm.Width), Height = ToDip(_sheetMm.Height), Background = Brushes.White };
        AddPage(page, face.Left, false, sizes);
        AddPage(page, face.Right, true, sizes);
        page.Measure(new Size(page.Width, page.Height));
        page.Arrange(new Rect(0, 0, page.Width, page.Height));

        var content = new PageContent();
        ((IAddChild)content).AddChild(page);
        return content;
    }

    private void AddPage(FixedPage page, BookPage bookPage, bool rightHalf, IReadOnlyDictionary<string, SizeD> sizes)
    {
        if (bookPage.Path is null)
        {
            return;
        }

        var size = sizes[bookPage.Path];
        var rect = HalfSheetLayout.Place(size, _sheetMm, rightHalf, marginMm);
        var image = ImageDecoder.Decode(bookPage.Path, TargetPixelWidth(rect.Width, size.Width)) ?? throw Unreadable(bookPage.Path);
        var element = new Image
        {
            Source = image,
            Stretch = Stretch.Fill,
            Width = ToDip(rect.Width),
            Height = ToDip(rect.Height),
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
        return new PrintFailedException($"Could not read the image {Path.GetFileName(path)}. Nothing was printed.");
    }

    private static double ToDip(double millimetres)
    {
        return millimetres * Paper.DipsPerMillimetre;
    }
}
