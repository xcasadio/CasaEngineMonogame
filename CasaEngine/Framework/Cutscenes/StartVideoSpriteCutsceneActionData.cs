namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Shows a named entity, starts the video of its VideoSpriteComponent from the first picture and returns at once
/// (ADR-0093). The component hides the entity in the update where the video ends. No end of the cutscene (stop,
/// replacement, failure) touches the video: only clearing the world stops it. The entity's root component must be a
/// VideoSpriteComponent.
/// </summary>
public sealed class StartVideoSpriteCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.StartVideoSprite;

    /// <summary>The entity's name. Null when the file does not say, which the validator refuses.</summary>
    public string EntityName { get; set; }

    /// <summary>What the loader found malformed, by JSON key. The validator reports these.</summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
