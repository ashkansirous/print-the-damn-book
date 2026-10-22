using System.Windows;
using System.Windows.Media;

namespace PrintTheDamnBook.App.Views;

/// <summary>Attached properties the Nocturne control templates read.</summary>
public static class Ui
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached("Icon", typeof(string), typeof(Ui), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TrailingIconProperty =
        DependencyProperty.RegisterAttached("TrailingIcon", typeof(string), typeof(Ui), new PropertyMetadata(string.Empty));

    /// <summary>Set from non-client mouse messages, where the normal mouse-over does not fire (the maximize button).</summary>
    public static readonly DependencyProperty IsPointerOverProperty =
        DependencyProperty.RegisterAttached("IsPointerOver", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    public static readonly DependencyProperty IsDropTargetProperty =
        DependencyProperty.RegisterAttached("IsDropTarget", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    public static readonly DependencyProperty IsDragSourceProperty =
        DependencyProperty.RegisterAttached("IsDragSource", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    public static readonly DependencyProperty HoverBrushProperty =
        DependencyProperty.RegisterAttached("HoverBrush", typeof(Brush), typeof(Ui), new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty PressedBrushProperty =
        DependencyProperty.RegisterAttached("PressedBrush", typeof(Brush), typeof(Ui), new PropertyMetadata(Brushes.Transparent));

    public static Brush GetHoverBrush(DependencyObject element) => (Brush)element.GetValue(HoverBrushProperty);

    public static void SetHoverBrush(DependencyObject element, Brush value) => element.SetValue(HoverBrushProperty, value);

    public static Brush GetPressedBrush(DependencyObject element) => (Brush)element.GetValue(PressedBrushProperty);

    public static void SetPressedBrush(DependencyObject element, Brush value) => element.SetValue(PressedBrushProperty, value);

    public static string GetIcon(DependencyObject element) => (string)element.GetValue(IconProperty);

    public static void SetIcon(DependencyObject element, string value) => element.SetValue(IconProperty, value);

    public static string GetTrailingIcon(DependencyObject element) => (string)element.GetValue(TrailingIconProperty);

    public static void SetTrailingIcon(DependencyObject element, string value) => element.SetValue(TrailingIconProperty, value);

    public static bool GetIsPointerOver(DependencyObject element) => (bool)element.GetValue(IsPointerOverProperty);

    public static void SetIsPointerOver(DependencyObject element, bool value) => element.SetValue(IsPointerOverProperty, value);

    public static bool GetIsDropTarget(DependencyObject element) => (bool)element.GetValue(IsDropTargetProperty);

    public static void SetIsDropTarget(DependencyObject element, bool value) => element.SetValue(IsDropTargetProperty, value);

    public static bool GetIsDragSource(DependencyObject element) => (bool)element.GetValue(IsDragSourceProperty);

    public static void SetIsDragSource(DependencyObject element, bool value) => element.SetValue(IsDragSourceProperty, value);
}
