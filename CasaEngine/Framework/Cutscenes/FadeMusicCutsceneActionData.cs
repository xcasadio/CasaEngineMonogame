namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Ramps the Music bus volume to a target.
/// Blocking: the cutscene waits for the ramp to finish, so the next action starts on the new
/// level. This is the one audio action that has a duration worth waiting for.
/// </summary>
public sealed class FadeMusicCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.FadeMusic;

    /// <summary>Target volume of the Music bus, in [0,1].</summary>
    public float TargetVolume { get; set; }

    /// <summary>Zero applies the target immediately.</summary>
    public float DurationSeconds { get; set; }
}
