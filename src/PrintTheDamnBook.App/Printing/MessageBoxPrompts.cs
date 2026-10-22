using System.Windows;

namespace PrintTheDamnBook.App.Printing;

public sealed class MessageBoxPrompts : IPrintPrompts
{
    private const string Caption = "Print the damn book";

    public void ShowError(string message)
    {
        MessageBox.Show(Application.Current.MainWindow, message, Caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool ConfirmContinue(string message)
    {
        var answer = MessageBox.Show(Application.Current.MainWindow, message, Caption, MessageBoxButton.OKCancel, MessageBoxImage.Information);
        return answer == MessageBoxResult.OK;
    }
}
