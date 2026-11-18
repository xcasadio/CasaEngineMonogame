namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Waits <see cref="Seconds"/> from the moment it starts, then plays its children in sequence. It is the same as a
/// <c>Sequence</c> that begins with a <c>Wait</c>, with the delay kept as data. In a <c>Parallel</c> all the <c>At</c>
/// actions start together, so <see cref="Seconds"/> is the time of each one inside that block.
/// </summary>
public sealed class AtCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.At;

    /// <summary>The delay in seconds; 0 when the file does not say. A negative value is refused by the validator.</summary>
    public float Seconds { get; set; }

    public List<CutsceneActionData> Actions { get; } = [];

    /// <summary>What the loader found malformed, by JSON key. The validator reports these.</summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
