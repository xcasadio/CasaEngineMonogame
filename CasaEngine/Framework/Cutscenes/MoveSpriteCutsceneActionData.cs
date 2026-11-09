namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Starts moving a named entity in a straight line to a target at a constant speed, then lets the sequence go on in
/// the same update: the motion advances with the elapsed game time, in the entity's update. The entity's root
/// component must be a StaticSpriteComponent. Replaces a motion in progress.
/// </summary>
public sealed class MoveSpriteCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.MoveSprite;

    /// <summary>The entity's name. Null when the file does not say, which the validator refuses.</summary>
    public string EntityName { get; set; }

    /// <summary>World X of the target. Null when the file does not say, which the validator refuses.</summary>
    public float? X { get; set; }

    /// <summary>World Y of the target. Null when the file does not say, which the validator refuses.</summary>
    public float? Y { get; set; }

    /// <summary>Speed in world units per second, strictly positive. Null when the file does not say, which the validator refuses.</summary>
    public float? Speed { get; set; }

    /// <summary>What the loader found malformed, by JSON key. The validator reports these.</summary>
    public Dictionary<string, string> FormatErrors { get; } = new();
}
