namespace CasaEngine.Framework.Dialogue.Runtime;

public sealed class DialogueStateChangedEventArgs : EventArgs
{
    public DialogueStateChangedEventArgs(DialogueRuntimeState previousState, DialogueRuntimeState currentState, DialogueLine currentLine)
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