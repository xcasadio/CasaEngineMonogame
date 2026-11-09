using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Makes a named entity visible, optionally changing its sprite, position, colour and blend mode first, and stops
/// any move or tint in progress on it. Instant: the sequence goes on in the same update. The entity's root
/// component must be a StaticSpriteComponent. A field left out keeps the sprite component's current value.
/// </summary>
public sealed class ShowSpriteCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.ShowSprite;

    /// <summary>The entity's name. Null when the file does not say, which the validator refuses.</summary>
    public string EntityName { get; set; }

    /// <summary>Name of a sprite asset in the catalogue. Null keeps the current sprite.</summary>
    public string Sprite { get; set; }

    /// <summary>World X of the entity. Null keeps the current one; given together with <see cref="Y"/>.</summary>
    public float? X { get; set; }

    /// <summary>World Y of the entity. Null keeps the current one; given together with <see cref="X"/>.</summary>
    public float? Y { get; set; }

    /// <summary>The sprite's colour. Null keeps the current one.</summary>
    public Color? Color { get; set; }

    /// <summary>The sprite's blend mode. Null keeps the current one.</summary>
    public SpriteBlendMode? BlendMode { get; set; }

    /// <summary>
    /// What the loader found malformed, by JSON key: a field left out stays null and is not listed here.
    /// The validator reports these.
    /// </summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
