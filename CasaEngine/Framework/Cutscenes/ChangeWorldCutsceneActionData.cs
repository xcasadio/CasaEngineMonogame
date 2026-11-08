namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Asks the game to load another world, then ends at once. The world changes at the next game update, which stops
/// this cutscene if it still plays. The play fails when the world is not in the catalogue.
/// </summary>
public sealed class ChangeWorldCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.ChangeWorld;

    /// <summary>The world's file name. Null or blank when the file does not say, which the validator refuses.</summary>
    public string Target { get; set; }
}
