namespace PrintTheDamnBook.App.ViewModels;

public enum WizardStep
{
    Open,
    Check,
    Print,
    Done,
}

/// <summary>Which arrangement the Check step shows.</summary>
public enum BookView
{
    Reading,
    Sheets,
}

public enum StepState
{
    Todo,
    Current,
    Done,
}

public enum FooterTone
{
    Normal,
    Accent,
    Warning,
}

/// <summary>One entry of the left rail.</summary>
public sealed record RailStepViewModel(int Index, int Number, string Title, string Summary, StepState State, bool IsReachable, string? Tooltip = null)
{
    public bool IsCurrent => State == StepState.Current;

    public bool IsDone => State == StepState.Done;

    public bool IsTodo => State == StepState.Todo;
}

/// <summary>A row of the reading-order view: a caption and up to two facing pages (null leaves the slot empty).</summary>
public sealed record SpreadViewModel(string Caption, PageViewModel? Left, PageViewModel? Right);

/// <summary>One sheet of the stack preview, offset from the front sheet in DIPs.</summary>
public sealed record StackLayerViewModel(double X, double Y);

public sealed record FoldStepViewModel(string Number, string Title, string Body);
