namespace PrintTheDamnBook.App.Printing;

/// <summary>Messages the print flow shows the user; kept behind an interface so the flow has no UI calls.</summary>
public interface IPrintPrompts
{
    void ShowError(string message);

    /// <summary>Asks the user to load the cover paper; false means they cancelled the rest of the print.</summary>
    bool ConfirmContinue(string message);
}
