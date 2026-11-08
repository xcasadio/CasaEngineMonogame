namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Opens a data-driven screen on the active UI view, the way the project's first screen is opened. Under external
/// view management (the editor) it opens nothing. The play fails when the screen is not in the catalogue or no view
/// hosts a UI.
/// </summary>
public sealed class OpenScreenCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.OpenScreen;

    /// <summary>The screen asset's id or name. Null or blank when the file does not say, which the validator refuses.</summary>
    public string Target { get; set; }
}
