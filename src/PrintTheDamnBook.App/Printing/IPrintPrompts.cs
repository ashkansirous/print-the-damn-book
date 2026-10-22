namespace PrintTheDamnBook.App.Printing;

/// <summary>Messages the print flow shows the user; kept behind an interface so the flow has no UI calls.</summary>
public interface IPrintPrompts
{
    void ShowError(string message);

    /// <summary>Asks the user to load the cover paper; false means they cancelled the rest of the print.</summary>
    bool ConfirmContinue(string message);

    /// <summary>Shows the reload instructions between the passes of manual two-sided printing; false means they cancelled the backs.</summary>
    bool ConfirmPrintBacks(string message);

    /// <summary>Asks whether the printed back of the test sheet is upside down compared with its front.</summary>
    bool AskBackUpsideDown();

    /// <summary>Reports that the backs failed after the fronts printed; true means try the backs again.</summary>
    bool ConfirmRetryBacks(string message);
}
