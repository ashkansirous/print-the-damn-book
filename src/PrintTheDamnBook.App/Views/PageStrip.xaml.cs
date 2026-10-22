using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using PrintTheDamnBook.App.ViewModels;
using PrintTheDamnBook.Core;

namespace PrintTheDamnBook.App.Views;

/// <summary>The page strip: raises reorder, remove and add intents to <see cref="MainViewModel"/>; the rules live in Core.</summary>
public partial class PageStrip : UserControl
{
    private const string DragSurface = "DragSurface";
    private const string DragFormat = "PrintTheDamnBook.Page";
    private const double EdgeZone = 40;
    private const double ScrollStep = 24;
    private const string ImageFilter = "Page images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";

    private static readonly Brush DropBrush = CreateDropBrush();

    private Point _dragStart;
    private PageViewModel? _dragCandidate;

    public PageStrip()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private static Brush CreateDropBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(90, 0, 120, 215));
        brush.Freeze();
        return brush;
    }

    private async void OnAddPagesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Add pages", Multiselect = true, Filter = ImageFilter };
        if (ViewModel is { } viewModel && dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            await viewModel.AddPagesAsync(dialog.FileNames);
        }
    }

    private void OnItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        var item = ItemOf(sender);
        var draggable = item is { Source: not null, IsPinned: false } && !IsOnButton(e.OriginalSource as DependencyObject);
        _dragCandidate = draggable ? item : null;
    }

    private void OnItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragCandidate = null;
    }

    private void OnItemMouseLeave(object sender, MouseEventArgs e)
    {
        _dragCandidate = null;
    }

    private static bool IsOnButton(DependencyObject? element)
    {
        while (element is not null and not Border { Name: DragSurface })
        {
            if (element is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    private void OnItemMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragCandidate is not { Source: { } page } || !ReferenceEquals(ItemOf(sender), _dragCandidate) || !MovedFarEnough(e.GetPosition(null)))
        {
            return;
        }

        _dragCandidate = null;
        var data = new DataObject(DragFormat, page);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
    }

    private bool MovedFarEnough(Point position) =>
        Math.Abs(position.X - _dragStart.X) > SystemParameters.MinimumHorizontalDragDistance
        || Math.Abs(position.Y - _dragStart.Y) > SystemParameters.MinimumVerticalDragDistance;

    private void OnItemDragOver(object sender, DragEventArgs e)
    {
        var allowed = CanDrop(sender, e);
        e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None;
        SetHighlight(sender, allowed);
        AutoScroll(e.GetPosition(StripScroller));
        e.Handled = true;
    }

    /// <summary>Scrolls the strip while the cursor is near its left or right edge, so off-screen slots can be reached.</summary>
    private void AutoScroll(Point position)
    {
        if (position.X < EdgeZone)
        {
            StripScroller.ScrollToHorizontalOffset(StripScroller.HorizontalOffset - ScrollStep);
        }
        else if (position.X > StripScroller.ActualWidth - EdgeZone)
        {
            StripScroller.ScrollToHorizontalOffset(StripScroller.HorizontalOffset + ScrollStep);
        }
    }

    private void OnItemDragLeave(object sender, DragEventArgs e)
    {
        SetHighlight(sender, false);
    }

    private void OnItemDrop(object sender, DragEventArgs e)
    {
        SetHighlight(sender, false);
        if (CanDrop(sender, e) && e.Data.GetData(DragFormat) is BookPage page)
        {
            MoveOnto(page, ItemOf(sender));
        }

        e.Handled = true;
    }

    /// <summary>A real page is a target slot; a trailing blank sends the page to the end of the real pages.</summary>
    private void MoveOnto(BookPage page, PageViewModel? target)
    {
        if (target?.Source is { } slot)
        {
            ViewModel?.MovePage(page, slot);
        }
        else
        {
            ViewModel?.MovePageToEnd(page);
        }
    }

    private static bool CanDrop(object sender, DragEventArgs e)
    {
        return e.Data.GetDataPresent(DragFormat) && ItemOf(sender) is { IsPinned: false };
    }

    private static void SetHighlight(object sender, bool on)
    {
        if (sender is Border border)
        {
            border.Background = on ? DropBrush : Brushes.Transparent;
        }
    }

    private static PageViewModel? ItemOf(object sender) => (sender as FrameworkElement)?.DataContext as PageViewModel;
}
