namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Opens a data-driven screen that holds one or more <c>Video</c> elements, waits until each of them has ended or has
/// been skipped, then removes the screen. It opens the screen the way <see cref="OpenScreenCutsceneActionData"/> does,
/// with the same failures and the same rule under external view management (it opens nothing and ends at once). The
/// play fails when the screen has no <c>Video</c> element.
/// </summary>
public sealed class PlayVideoCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.PlayVideo;

    /// <summary>The screen asset's id or name. Null or blank when the file does not say, which the validator refuses.</summary>
    public string Target { get; set; }
}
