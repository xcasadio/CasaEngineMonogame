using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Starts changing the colour of a named entity's sprite linearly to a target over a duration, then lets the
/// sequence go on in the same update: the tint advances with the elapsed game time, in the entity's update. The
/// entity's root component must be a StaticSpriteComponent. Replaces a tint in progress.
/// </summary>
public sealed class TintSpriteCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.TintSprite;

    /// <summary>The entity's name. Null when the file does not say, which the validator refuses.</summary>
    public string EntityName { get; set; }

    /// <summary>The target colour. Null when the file does not say, which the validator refuses.</summary>
    public Color? Color { get; set; }

    /// <summary>Duration in seconds, at least 0. Null when the file does not say, which the validator refuses.</summary>
    public float? Duration { get; set; }

    /// <summary>What the loader found malformed, by JSON key. The validator reports these.</summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
