using CasaEngine.Framework.Dialogue.Presentation;

namespace CasaEngine.Framework.Dialogue.Runtime;

public sealed class DialogueService : IDialoguePresenter
{
    private static readonly IReadOnlyList<string> NoChoices = Array.Empty<string>();

    public DialogueRuntimeState State { get; private set; } = DialogueRuntimeState.Closed;
    public DialogueLine CurrentLine { get; private set; } = DialogueLine.Empty;
    public bool IsOpen => State != DialogueRuntimeState.Closed;

    public IReadOnlyList<string> Choices { get; private set; } = NoChoices;
    public bool HasChoices => Choices.Count > 0;

    public event EventHandler<DialogueStateChangedEventArgs> StateChanged;
    public event EventHandler<DialoguePresentationChangedEventArgs> PresentationChanged;
    public event EventHandler<DialogueChoiceSelectedEventArgs> ChoiceSelected;

    public bool TryOpen(string text)
    {
        return TryOpen(new DialogueLine(text));
    }

    public bool TryOpen(DialogueLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (IsOpen)
        {
            return false;
        }

        ChangeState(DialogueRuntimeState.Open, line);
        return true;
    }

    public bool ShowLine(DialogueLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (!IsOpen)
        {
            ChangeState(DialogueRuntimeState.Open, line);
            return true;
        }

        DialogueRuntimeState previousState = State;
        State = DialogueRuntimeState.Open;
        CurrentLine = line;
        Choices = NoChoices;
        RaisePresentationChanged(previousState);
        return true;
    }

    public bool ShowChoices(IReadOnlyList<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        if (labels.Count == 0)
        {
            throw new ArgumentException("A choice list must contain at least one label.", nameof(labels));
        }

        DialogueRuntimeState previousState = State;

        // Defensive copy: the caller's list must not be able to mutate our state afterwards,
        // and a fresh array guarantees no stale references survive across calls.
        Choices = new List<string>(labels).AsReadOnly();
        State = DialogueRuntimeState.AwaitingChoice;
        StateChanged?.Invoke(this, new DialogueStateChangedEventArgs(previousState, State, CurrentLine));
        RaisePresentationChanged(previousState);
        return true;
    }

    public bool SelectChoice(int index)
    {
        if (State != DialogueRuntimeState.AwaitingChoice)
        {
            return false;
        }

        if (index < 0 || index >= Choices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside the current choice list.");
        }

        IReadOnlyList<string> selectedLabels = Choices;

        DialogueRuntimeState previousState = State;
        Choices = NoChoices;
        State = DialogueRuntimeState.Open;
        StateChanged?.Invoke(this, new DialogueStateChangedEventArgs(previousState, State, CurrentLine));
        RaisePresentationChanged(previousState);

        ChoiceSelected?.Invoke(this, new DialogueChoiceSelectedEventArgs(index, selectedLabels));
        return true;
    }

    public bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

        Choices = NoChoices;
        ChangeState(DialogueRuntimeState.Closed, DialogueLine.Empty);
        return true;
    }

    private void ChangeState(DialogueRuntimeState newState, DialogueLine line)
    {
        DialogueRuntimeState previousState = State;
        State = newState;
        CurrentLine = line;
        StateChanged?.Invoke(this, new DialogueStateChangedEventArgs(previousState, State, CurrentLine));
        RaisePresentationChanged(previousState);
    }

    private void RaisePresentationChanged(DialogueRuntimeState previousState)
    {
        PresentationChanged?.Invoke(this, new DialoguePresentationChangedEventArgs(previousState, State, CurrentLine));
    }
}
