namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Describes how a started cutscene play ended.
/// </summary>
public sealed class CutsceneEndedEventArgs : EventArgs
{
    public CutsceneEndedEventArgs(CutsceneRuntimeState state, CutsceneAsset asset)
    {
        State = state;
        Asset = asset;
    }

    /// <summary>Final state: <see cref="CutsceneRuntimeState.Completed"/>, <see cref="CutsceneRuntimeState.Stopped"/> or <see cref="CutsceneRuntimeState.Failed"/>.</summary>
    public CutsceneRuntimeState State { get; }

    /// <summary>The asset of the play that ended.</summary>
    public CutsceneAsset Asset { get; }
}
