namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Shows a named entity, starts the video of its VideoSpriteComponent from the first picture, waits until the video has
/// ended, then hides the entity (ADR-0130). The entity's root component must be a VideoSpriteComponent. When the
/// sequence stops before the end, the video is stopped and the entity hidden too.
/// </summary>
public sealed class PlayVideoSpriteCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.PlayVideoSprite;

    /// <summary>The entity's name. Null when the file does not say, which the validator refuses.</summary>
    public string EntityName { get; set; }

    /// <summary>What the loader found malformed, by JSON key. The validator reports these.</summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
