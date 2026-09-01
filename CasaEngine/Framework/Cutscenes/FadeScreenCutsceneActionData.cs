using CasaEngine.Framework.Rendering.Depth;

namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Ramps the screen fade/tint overlay to a target colour.
/// Blocking: the cutscene waits for the ramp to finish, so the next action starts once the screen
/// has reached the new colour. Mirrors <see cref="FadeMusicCutsceneActionData"/> exactly - the one
/// screen-effect action that has a duration worth waiting for.
/// </summary>
public sealed class FadeScreenCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.FadeScreen;

    /// <summary>Target red channel of the overlay, 0-255.</summary>
    public byte R { get; set; }

    /// <summary>Target green channel of the overlay, 0-255.</summary>
    public byte G { get; set; }

    /// <summary>Target blue channel of the overlay, 0-255.</summary>
    public byte B { get; set; }

    /// <summary>Zero applies the target immediately.</summary>
    public float DurationSeconds { get; set; }

    /// <summary>Blend mode the overlay is drawn with while active.</summary>
    public SpriteBlendMode BlendMode { get; set; }
}
