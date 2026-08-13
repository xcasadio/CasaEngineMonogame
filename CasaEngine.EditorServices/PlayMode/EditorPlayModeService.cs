namespace CasaEngine.EditorServices.PlayMode;

/// <summary>
/// State machine of the editor play mode: Editing → Starting → Playing ⇄ Paused → Stopping → Editing.
/// Transitions are refused (return false) instead of throwing when the current state does not allow them.
/// </summary>
public sealed class EditorPlayModeService
{
    private readonly IEditorPlaySessionController _controller;

    public EditorPlayModeState State { get; private set; } = EditorPlayModeState.Editing;

    public event EventHandler<EditorPlayModeStateChangedEventArgs>? StateChanged;

    public bool IsPlaySessionActive => State != EditorPlayModeState.Editing;

    public EditorPlayModeService(IEditorPlaySessionController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        _controller = controller;
    }

    public bool TryStartPlay()
    {
        if (State != EditorPlayModeState.Editing)
        {
            return false;
        }

        SetState(EditorPlayModeState.Starting);

        bool started;
        try
        {
            started = _controller.StartSession();
        }
        catch
        {
            SetState(EditorPlayModeState.Editing);
            throw;
        }

        if (!started)
        {
            SetState(EditorPlayModeState.Editing);
            return false;
        }

        SetState(EditorPlayModeState.Playing);
        return true;
    }

    public bool TryStopPlay()
    {
        if (State is not (EditorPlayModeState.Playing or EditorPlayModeState.Paused))
        {
            return false;
        }

        SetState(EditorPlayModeState.Stopping);
        try
        {
            _controller.StopSession();
        }
        finally
        {
            // Even when the teardown fails the session is over: staying in Stopping
            // would lock the editor out of both editing and a fresh play session.
            SetState(EditorPlayModeState.Editing);
        }

        return true;
    }

    public bool TryPause()
    {
        if (State != EditorPlayModeState.Playing)
        {
            return false;
        }

        _controller.SetPaused(true);
        SetState(EditorPlayModeState.Paused);
        return true;
    }

    public bool TryResume()
    {
        if (State != EditorPlayModeState.Paused)
        {
            return false;
        }

        _controller.SetPaused(false);
        SetState(EditorPlayModeState.Playing);
        return true;
    }

    public bool TogglePlayStop()
    {
        return State == EditorPlayModeState.Editing ? TryStartPlay() : TryStopPlay();
    }

    public bool TogglePause()
    {
        return State switch
        {
            EditorPlayModeState.Playing => TryPause(),
            EditorPlayModeState.Paused => TryResume(),
            _ => false,
        };
    }

    private void SetState(EditorPlayModeState newState)
    {
        if (State == newState)
        {
            return;
        }

        var previousState = State;
        State = newState;
        StateChanged?.Invoke(this, new EditorPlayModeStateChangedEventArgs(previousState, newState));
    }
}
