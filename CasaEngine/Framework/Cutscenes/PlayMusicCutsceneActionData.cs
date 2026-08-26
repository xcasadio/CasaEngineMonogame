namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Starts a streamed track, optionally crossfading from the one currently playing.
/// Non blocking: the cutscene moves on while the music comes in.
/// </summary>
public sealed class PlayMusicCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.PlayMusic;

    /// <summary>Id of the <c>.sound</c> asset to stream. It must be marked as streaming.</summary>
    public Guid SoundAssetId { get; set; } = Guid.Empty;

    /// <summary>Zero starts at full volume, otherwise the track fades in.</summary>
    public float FadeInSeconds { get; set; }

    /// <summary>
    /// True fades the current track out over <see cref="FadeInSeconds"/> while the new one comes
    /// in. False leaves whatever was playing untouched.
    /// </summary>
    public bool Crossfade { get; set; } = true;
}
