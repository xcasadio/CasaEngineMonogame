namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Waits a number of game updates (frames) instead of seconds: the elapsed time and the time scale are ignored.
/// A count of zero ends the action at once, so the next action runs in the same update.
/// </summary>
public sealed class WaitFramesCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.WaitFrames;

    /// <summary>Number of updates to wait. Null when the file does not say, which the validator refuses.</summary>
    public int? Frames { get; set; }

    /// <summary>
    /// What the loader found malformed, by JSON key: a field left out stays null and is not listed here.
    /// The validator reports these.
    /// </summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
