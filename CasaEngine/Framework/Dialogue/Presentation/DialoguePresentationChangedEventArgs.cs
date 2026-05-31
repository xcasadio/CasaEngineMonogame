using CasaEngine.Framework.Dialogue.Runtime;

namespace CasaEngine.Framework.Dialogue.Presentation;

public sealed class DialoguePresentationChangedEventArgs : EventArgs
{
    public DialoguePresentationChangedEventArgs(DialogueRuntimeState previousState, DialogueRuntimeState currentState, DialogueLine currentLine)
    {
        ArgumentNullException.ThrowIfNull(currentLine);

        PreviousState = previousState;
        CurrentState = currentState;
        CurrentLine = currentLine;
    }

    public DialogueRuntimeState PreviousState { get; }
    public DialogueRuntimeState CurrentState { get; }
    public DialogueLine CurrentLine { get; }
}