using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Cutscenes;

public sealed class MoveToCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.MoveTo;

    public string EntityName { get; set; } = string.Empty;

    public Vector3 Destination { get; set; }

    public float StoppingDistance { get; set; } = 0.1f;

    public float TimeoutSeconds { get; set; }
}