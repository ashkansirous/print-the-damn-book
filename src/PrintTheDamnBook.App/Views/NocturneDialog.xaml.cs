using System.Windows;
using System.Windows.Automation;

namespace PrintTheDamnBook.App.Views;

/// <summary>A modal Nocturne dialog with a primary and an optional secondary (cancel) button. ShowDialog returns true for the primary.</summary>
public partial class NocturneDialog : Window
{
    private const double WideWidth = 640;

    /// <summary>Wide dialogs (long text, such as licences) get a wider card; the text scrolls when it is taller than the window.</summary>
    public NocturneDialog(Window owner, string title, string message, string primaryLabel, string? secondaryLabel, bool wide = false)
    {
        InitializeComponent();
        Owner = owner;
        Title = title;
        AutomationProperties.SetName(this, title);
        TitleText.Text = title;
        BodyText.Text = message;
        PrimaryButton.Content = primaryLabel;
        AutomationProperties.SetName(PrimaryButton, primaryLabel);
        SecondaryButton.Content = secondaryLabel;
        SecondaryButton.Visibility = secondaryLabel is null ? Visibility.Collapsed : Visibility.Visible;
        SecondaryButton.IsCancel = secondaryLabel is not null;
        PrimaryButton.IsCancel = secondaryLabel is null;
        Card.MaxWidth = wide ? WideWidth : Card.MaxWidth;
        CoverOwner(owner);
    }

    private void CoverOwner(Window owner)
    {
        if (owner.WindowState == WindowState.Minimized)
        {
            CenterOnRestoreBounds(owner);
            return;
        }

        var origin = owner.PointToScreen(new Point(0, 0));
        var source = PresentationSource.FromVisual(owner);
        var dips = source?.CompositionTarget?.TransformFromDevice.Transform(origin) ?? origin;
        Left = dips.X;
        Top = dips.Y;
        Width = owner.ActualWidth;
        Height = owner.ActualHeight;
    }

    /// <summary>A minimized owner has no on-screen position; cover where it will reappear.</summary>
    private void CenterOnRestoreBounds(Window owner)
    {
        var bounds = owner.RestoreBounds;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
    }

    private void OnPrimaryClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnSecondaryClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
