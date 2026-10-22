using System.Windows;
using System.Windows.Controls;

namespace PrintTheDamnBook.App.Printing;

public sealed class MessageBoxPrompts : IPrintPrompts
{
    private const string Caption = "Print the damn book";
    private const string PrintBacksLabel = "Print the backs";
    private const string CancelLabel = "Cancel";
    private const string UpsideDownQuestion = "Wait until the printer has stopped and look at the test sheet.\n\nIs the back upside down compared with the front?";
    private const double DialogWidth = 440;
    private const double DialogPadding = 16;
    private const double ButtonGap = 8;

    public void ShowError(string message)
    {
        MessageBox.Show(Application.Current.MainWindow, message, Caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool ConfirmContinue(string message)
    {
        var answer = MessageBox.Show(Application.Current.MainWindow, message, Caption, MessageBoxButton.OKCancel, MessageBoxImage.Information);
        return answer == MessageBoxResult.OK;
    }

    public bool ConfirmPrintBacks(string message)
    {
        return BuildReloadDialog(message).ShowDialog() == true;
    }

    public bool AskBackUpsideDown()
    {
        var answer = MessageBox.Show(Application.Current.MainWindow, UpsideDownQuestion, Caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return answer == MessageBoxResult.Yes;
    }

    public bool ConfirmRetryBacks(string message)
    {
        var answer = MessageBox.Show(Application.Current.MainWindow, message, Caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return answer == MessageBoxResult.Yes;
    }

    private static Window BuildReloadDialog(string message)
    {
        var dialog = new Window
        {
            Title = Caption,
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = DialogWidth,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
        };
        var print = new Button { Content = PrintBacksLabel, IsDefault = true, Padding = new Thickness(14, 6, 14, 6) };
        var cancel = new Button { Content = CancelLabel, IsCancel = true, Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(ButtonGap, 0, 0, 0) };
        print.Click += (_, _) => dialog.DialogResult = true;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DialogPadding, 0, 0) };
        buttons.Children.Add(print);
        buttons.Children.Add(cancel);
        var content = new StackPanel { Margin = new Thickness(DialogPadding) };
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(buttons);
        dialog.Content = content;
        return dialog;
    }
}
