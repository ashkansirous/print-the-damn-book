using System.Windows;
using PrintTheDamnBook.App.Views;

namespace PrintTheDamnBook.App.Printing;

/// <summary>The print flow's messages as Nocturne dialogs over the main window.</summary>
public sealed class NocturnePrompts : IPrintPrompts
{
    private const string ErrorTitle = "Could not print";
    private const string CoverTitle = "Cover's away";
    private const string ReloadTitle = "Put the stack back";
    private const string UpsideDownTitle = "Which way up?";
    private const string RetryTitle = "The backs did not print";
    private const string OkLabel = "OK";
    private const string CancelLabel = "Cancel";
    private const string ContinueLabel = "OK, print the rest";
    private const string PrintBacksLabel = "Print the backs";
    private const string YesLabel = "Yes";
    private const string NoLabel = "No";
    private const string TryAgainLabel = "Try again";
    private const string UpsideDownQuestion = "Wait until the printer has stopped and look at the test sheet.\n\nIs the back upside down compared with the front?";

    public void ShowError(string message)
    {
        Ask(ErrorTitle, message, OkLabel, null);
    }

    public bool ConfirmContinue(string message)
    {
        return Ask(CoverTitle, message, ContinueLabel, CancelLabel);
    }

    public bool ConfirmPrintBacks(string message)
    {
        return Ask(ReloadTitle, message, PrintBacksLabel, CancelLabel);
    }

    public bool AskBackUpsideDown()
    {
        return Ask(UpsideDownTitle, UpsideDownQuestion, YesLabel, NoLabel);
    }

    public bool ConfirmRetryBacks(string message)
    {
        return Ask(RetryTitle, message, TryAgainLabel, NoLabel);
    }

    private static bool Ask(string title, string message, string primary, string? secondary)
    {
        return new NocturneDialog(Application.Current.MainWindow, title, message, primary, secondary).ShowDialog() == true;
    }
}
