namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Plays a sound effect. Non blocking: the cutscene moves on immediately, because a cutscene
/// usually wants a sound over an action, not instead of it.
/// </summary>
public sealed class PlaySoundCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.PlaySound;

    /// <summary>Id of the <c>.sound</c> asset to play.</summary>
    public Guid SoundAssetId { get; set; } = Guid.Empty;

    /// <summary>Scales the asset volume, in [0,1].</summary>
    public float Volume { get; set; } = 1f;

    /// <summary>Empty keeps the bus declared by the asset.</summary>
    public string BusName { get; set; } = string.Empty;
}
