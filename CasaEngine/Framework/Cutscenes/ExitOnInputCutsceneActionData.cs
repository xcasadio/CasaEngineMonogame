namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Arms a watch for the rest of the cutscene, then lets the sequence go on in the same update. When one of the named
/// inputs of a buttons mapping is pressed (the press of the frame, not a held button), the cutscene is stopped and the
/// next cutscene is played. A new ExitOnInput replaces the watch in progress. The watch never outlives its play.
/// </summary>
public sealed class ExitOnInputCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.ExitOnInput;

    /// <summary>Name or id of a catalogued buttons mapping. Null when the file does not say, which the validator refuses.</summary>
    public string Mapping { get; set; }

    /// <summary>The names of the inputs of the mapping that leave the cutscene. At least one.</summary>
    public List<string> Inputs { get; } = [];

    /// <summary>Name or id of the catalogued cutscene played on the press. Null when the file does not say, which the validator refuses.</summary>
    public string NextCutscene { get; set; }

    /// <summary>What the loader found malformed, by JSON key. The validator reports these.</summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
