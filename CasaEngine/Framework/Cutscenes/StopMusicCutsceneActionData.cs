namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Stops every streamed track. Non blocking, even with a fade out: the cutscene does not wait
/// for the silence.
/// </summary>
public sealed class StopMusicCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.StopMusic;

    /// <summary>Zero stops immediately, otherwise the tracks fade out.</summary>
    public float FadeOutSeconds { get; set; }
}
