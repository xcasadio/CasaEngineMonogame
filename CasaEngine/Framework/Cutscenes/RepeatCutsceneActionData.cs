namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Plays its children in sequence, again and again: <see cref="Count"/> laps, or without end when it is 0. An endless
/// lap that ends without a frame passing fails the play, so that a loop of instant actions cannot freeze the game.
/// </summary>
public sealed class RepeatCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.Repeat;

    /// <summary>The number of laps, 0 for no end. Null when the file does not say, which the validator refuses.</summary>
    public int? Count { get; set; }

    public List<CutsceneActionData> Actions { get; } = [];

    /// <summary>What the loader found malformed, by JSON key. The validator reports these.</summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
