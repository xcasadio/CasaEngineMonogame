namespace CasaEngine.EditorServices.PlayMode;

/// <summary>
/// Host-side actions of a play session. The editor implements the actual work
/// (world snapshot, policy switch, camera/input binding); the state machine in
/// <see cref="EditorPlayModeService"/> only decides when each action is allowed.
/// </summary>
public interface IEditorPlaySessionController
{
    /// <summary>
    /// Starts the play session. Returns false when the session could not start
    /// (for example a script build failure); the service then returns to Editing.
    /// </summary>
    bool StartSession();

    void StopSession();

    void SetPaused(bool paused);
}
