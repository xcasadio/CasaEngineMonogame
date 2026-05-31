using CasaEngine.Framework.Dialogue.Presentation;

namespace CasaEngine.Framework.Dialogue.Runtime;

public sealed class DialogueService : IDialoguePresenter
{
    public DialogueRuntimeState State { get; private set; } = DialogueRuntimeState.Closed;
    public DialogueLine CurrentLine { get; private set; } = DialogueLine.Empty;
    public bool IsOpen => State == DialogueRuntimeState.Open;

    public event EventHandler<DialogueStateChangedEventArgs> StateChanged;
    public event EventHandler<DialoguePresentationChangedEventArgs> PresentationChanged;

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
        CurrentLine = line;
        RaisePresentationChanged(previousState);
        return true;
    }

    public bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

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