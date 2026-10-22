using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PrintTheDamnBook.App.ViewModels;
using PrintTheDamnBook.Core;

namespace PrintTheDamnBook.App.Views;

/// <summary>The Check step: raises reorder intents to <see cref="MainViewModel"/>; the rules live in Core.</summary>
public partial class CheckStepView : UserControl
{
    private const string DragSurface = "DragSurface";
    private const string DragFormat = "PrintTheDamnBook.Page";
    private const double EdgeZone = 60;
    private const double ScrollStep = 24;

    private Point _dragStart;
    private PageViewModel? _dragCandidate;
    private BindingBase? _tooltipBinding;

    public CheckStepView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnTileMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        var item = ItemOf(sender);
        var draggable = item is { Source: not null, IsPinned: false } && !IsOnButton(e.OriginalSource as DependencyObject);
        _dragCandidate = draggable ? item : null;
        if (item is not null && !IsOnButton(e.OriginalSource as DependencyObject))
        {
            // The enclosing ScrollViewer grabs focus later in the same click, so claim it after that.
            var tile = (UIElement)sender;
            Dispatcher.BeginInvoke(() => tile.Focus(), DispatcherPriority.Input);
        }
    }

    /// <summary>Alt+Left / Alt+Right moves the focused page one place earlier / later, then puts focus back on it.</summary>
    private void OnTileKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var offset = key == Key.Left ? -1 : key == Key.Right ? 1 : 0;
        var item = ItemOf(sender);
        var altHeld = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        if (!altHeld || offset == 0 || item is not { Source: { } page, IsPinned: false })
        {
            return;
        }

        e.Handled = true;
        if (ViewModel?.MovePageBy(page, offset) == true)
        {
            Dispatcher.BeginInvoke(() => FocusTileOf(page), DispatcherPriority.Loaded);
        }
    }

    private void FocusTileOf(BookPage page)
    {
        FindTile(this, page)?.Focus();
    }

    private static Border? FindTile(DependencyObject parent, BookPage page)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Border { Name: DragSurface, DataContext: PageViewModel { Source: { } source } } tile && ReferenceEquals(source, page))
            {
                return tile;
            }

            if (FindTile(child, page) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void OnTileMouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragCandidate = null;
    }

    private void OnTileMouseLeave(object sender, MouseEventArgs e)
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

    private void OnTileMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragCandidate is not { Source: { } page } || !ReferenceEquals(ItemOf(sender), _dragCandidate) || !MovedFarEnough(e.GetPosition(null)))
        {
            return;
        }

        _dragCandidate = null;
        var surface = (DependencyObject)sender;
        var tile = (FrameworkElement)sender;
        Ui.SetIsDragSource(surface, true);
        SetTooltipsEnabled(tile, false);
        try
        {
            DragDrop.DoDragDrop(surface, new DataObject(DragFormat, page), DragDropEffects.Move);
        }
        finally
        {
            Ui.SetIsDragSource(surface, false);
            SetTooltipsEnabled(tile, true);
        }
    }

    /// <summary>Removes the tile's tooltip for the length of a drag (which closes one already open) and restores it afterwards, so none is left on screen.</summary>
    private void SetTooltipsEnabled(FrameworkElement surface, bool enabled)
    {
        if (!enabled)
        {
            _tooltipBinding = BindingOperations.GetBindingBase(surface, FrameworkElement.ToolTipProperty);
            surface.ClearValue(FrameworkElement.ToolTipProperty);
        }
        else if (_tooltipBinding is not null)
        {
            BindingOperations.SetBinding(surface, FrameworkElement.ToolTipProperty, _tooltipBinding);
            _tooltipBinding = null;
        }
    }

    private bool MovedFarEnough(Point position) =>
        Math.Abs(position.X - _dragStart.X) > SystemParameters.MinimumHorizontalDragDistance
        || Math.Abs(position.Y - _dragStart.Y) > SystemParameters.MinimumVerticalDragDistance;

    private void OnTileDragOver(object sender, DragEventArgs e)
    {
        var allowed = CanDrop(sender, e);
        e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None;
        SetHighlight(sender, allowed);
        AutoScroll(e);
        e.Handled = true;
    }

    /// <summary>Scrolls the page while the cursor is near its top or bottom edge, so off-screen slots can be reached.</summary>
    private void AutoScroll(DragEventArgs e)
    {
        if (FindScroller() is not { } scroller)
        {
            return;
        }

        var y = e.GetPosition(scroller).Y;
        if (y < EdgeZone)
        {
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset - ScrollStep);
        }
        else if (y > scroller.ActualHeight - EdgeZone)
        {
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset + ScrollStep);
        }
    }

    private ScrollViewer? FindScroller()
    {
        DependencyObject? element = this;
        while (element is not null and not ScrollViewer)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        return element as ScrollViewer;
    }

    private void OnTileDragLeave(object sender, DragEventArgs e)
    {
        SetHighlight(sender, false);
    }

    private void OnTileDrop(object sender, DragEventArgs e)
    {
        SetHighlight(sender, false);
        if (CanDrop(sender, e) && e.Data.GetData(DragFormat) is BookPage page)
        {
            MoveOnto(page, ItemOf(sender));
        }

        e.Handled = true;
    }

    /// <summary>A real page is a target slot; a blank or the back cover sends the page to the end of the real pages.</summary>
    private void MoveOnto(BookPage page, PageViewModel? target)
    {
        if (target?.Source is { } slot && !target.IsPinned)
        {
            ViewModel?.MovePage(page, slot);
        }
        else
        {
            ViewModel?.MovePageToEnd(page);
        }
    }

    /// <summary>Nothing can be dropped on the front cover; the back cover takes a page in front of it.</summary>
    private static bool CanDrop(object sender, DragEventArgs e)
    {
        return e.Data.GetDataPresent(DragFormat) && ItemOf(sender) is { } target && (!target.IsPinned || target.Kind == PageKind.BackCover);
    }

    private static void SetHighlight(object sender, bool on)
    {
        if (sender is DependencyObject element)
        {
            Ui.SetIsDropTarget(element, on);
        }
    }

    private static PageViewModel? ItemOf(object sender) => (sender as FrameworkElement)?.DataContext as PageViewModel;
}
