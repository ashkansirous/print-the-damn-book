using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PrintTheDamnBook.App.ViewModels;

namespace PrintTheDamnBook.App.Views;

/// <summary>The "Stories from Dino" dialog (Slice 18). <see cref="DownloadedPath"/> is the finished <c>.dino</c> file once it returns true.</summary>
public partial class DinoStoreDialog : Window
{
    private readonly DinoStoreViewModel _viewModel;
    private bool _closed;

    public DinoStoreDialog(Window owner, DinoStoreViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Owner = owner;
        CoverOwner(owner);
        viewModel.CloseRequested += OnCloseRequested;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }

    /// <summary>The downloaded file, or null when the dialog was closed or cancelled.</summary>
    public string? DownloadedPath { get; private set; }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.Close();
        base.OnClosed(e);
    }

    private void OnCloseRequested(string? path)
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        DownloadedPath = path;
        DialogResult = path is not null;
    }

    /// <summary>Covers the whole owner window with the scrim, as <see cref="NocturneDialog"/> does.</summary>
    private void CoverOwner(Window owner)
    {
        if (owner.WindowState == WindowState.Minimized)
        {
            var bounds = owner.RestoreBounds;
            (Left, Top, Width, Height) = (bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            return;
        }

        var origin = owner.PointToScreen(new Point(0, 0));
        var source = PresentationSource.FromVisual(owner);
        var dips = source?.CompositionTarget?.TransformFromDevice.Transform(origin) ?? origin;
        (Left, Top, Width, Height) = (dips.X, dips.Y, owner.ActualWidth, owner.ActualHeight);
    }
}

/// <summary>Turns a width into a height by a fixed ratio (a 3:4 cover is 1.3333 times as tall as it is wide).</summary>
public sealed class AspectHeightConverter : IValueConverter
{
    public double Ratio { get; set; } = 1;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double width ? width * Ratio : 0d;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
