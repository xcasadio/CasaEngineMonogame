using System;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.EditorServices.PlayMode;
using CasaEngine.Framework.Application;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Editor.PlayMode;

/// <summary>
/// Editor-side implementation of a play session: plays a serialized copy of the edit
/// world under the EditorSimulation policy, and reinstalls the untouched edit world
/// when the session ends. The edit world is set aside without Clear(), so stopping
/// restores the exact editing state.
/// </summary>
internal sealed class EditorPlaySessionController : IEditorPlaySessionController
{
    private readonly Func<CasaEngineGame> _gameProvider;
    private World _editWorld;
    private World _playWorld;

    public World PlayWorld => _playWorld;

    /// <summary>Edit world set aside while the session runs; restored on stop.</summary>
    internal World HeldEditWorld => _editWorld;

    public event Action SessionStarted;
    public event Action SessionStopped;

    public EditorPlaySessionController(Func<CasaEngineGame> gameProvider)
    {
        ArgumentNullException.ThrowIfNull(gameProvider);
        _gameProvider = gameProvider;
    }

    public bool StartSession()
    {
        var game = _gameProvider();
        var editWorld = game?.GameManager.CurrentWorld;
        if (editWorld == null)
        {
            Logs.WriteWarning("Play mode: no world is loaded in the editor runtime.");
            return false;
        }

        _editWorld = editWorld;
        _playWorld = EditorWorldPlaySnapshot.CreatePlayWorld(editWorld);

        game.ExecutionPolicy = GameplayExecutionPolicies.EditorSimulation;
        game.GameManager.TimeScale = 1f;
        game.GameManager.SetWorldToLoad(_playWorld);

        Logs.WriteInfo($"Play mode started on world '{_playWorld.Name}'.");
        SessionStarted?.Invoke();
        return true;
    }

    public void StopSession()
    {
        var game = _gameProvider();
        try
        {
            // Every game sound dies with the session, including the ones no world owns.
            // The Editor bus is spared: an asset preview must survive Stop.
            game?.AudioSystemComponent?.Service.StopAllExceptBus(AudioBusNames.Editor);
            _playWorld?.Clear();
        }
        finally
        {
            if (game != null)
            {
                game.GameManager.TimeScale = 1f;
                game.ExecutionPolicy = GameplayExecutionPolicies.EditorPreview;

                if (_editWorld != null)
                {
                    game.GameManager.RestoreWorld(_editWorld);
                }
            }

            _playWorld = null;
            _editWorld = null;

            Logs.WriteInfo("Play mode stopped, edit world restored.");
            SessionStopped?.Invoke();
        }
    }

    public void SetPaused(bool paused)
    {
        var game = _gameProvider();
        if (game == null)
        {
            return;
        }

        game.GameManager.TimeScale = paused ? 0f : 1f;

        // A time scale of zero freezes the simulation but not the audio hardware: the voices
        // have to be paused explicitly. The Editor bus keeps playing.
        var audioService = game.AudioSystemComponent?.Service;
        if (paused)
        {
            audioService?.PauseAllExceptBus(AudioBusNames.Editor);
        }
        else
        {
            audioService?.ResumeAllExceptBus(AudioBusNames.Editor);
        }
    }
}
