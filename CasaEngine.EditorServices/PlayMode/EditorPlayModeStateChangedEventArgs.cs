namespace CasaEngine.EditorServices.PlayMode;

public sealed class EditorPlayModeStateChangedEventArgs : EventArgs
{
    public EditorPlayModeState PreviousState { get; }
    public EditorPlayModeState NewState { get; }

    public EditorPlayModeStateChangedEventArgs(EditorPlayModeState previousState, EditorPlayModeState newState)
    {
        PreviousState = previousState;
        NewState = newState;
    }
}
