namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Makes a named entity invisible and stops any move or tint in progress on it. Instant. The entity's root
/// component must be a StaticSpriteComponent.
/// </summary>
public sealed class HideSpriteCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.HideSprite;

    /// <summary>The entity's name. Null when the file does not say, which the validator refuses.</summary>
    public string EntityName { get; set; }

    /// <summary>What the loader found malformed, by JSON key. The validator reports these.</summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
