using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Ramps the screen overlay from one colour to another over a number of game updates (frames), whatever the
/// elapsed time. Blocking: the next action runs once the target has been drawn. Unlike
/// <see cref="FadeScreenCutsceneActionData"/>, it sets the layer and starts the ramp from an explicit colour,
/// so its result does not depend on what a previous fade left behind.
/// </summary>
public sealed class FadeScreenFramesCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.FadeScreenFrames;

    /// <summary>The overlay the ramp starts from. Null when the file does not say, which the validator refuses.</summary>
    public Color? From { get; set; }

    /// <summary>The overlay the ramp ends on. Null when the file does not say, which the validator refuses.</summary>
    public Color? To { get; set; }

    /// <summary>Number of updates of the ramp. Zero applies the target at once. Null when the file does not say.</summary>
    public int? Frames { get; set; }

    /// <summary>Where the overlay is drawn. Null when the file does not say.</summary>
    public ScreenEffectLayer? Layer { get; set; }

    /// <summary>How the overlay blends. Null when the file does not say.</summary>
    public SpriteBlendMode? BlendMode { get; set; }

    /// <summary>
    /// What the loader found malformed, by JSON key: a field left out stays null and is not listed here.
    /// The validator reports these.
    /// </summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
