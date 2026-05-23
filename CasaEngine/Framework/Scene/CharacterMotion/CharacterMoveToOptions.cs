using CasaEngine.Framework.Scene.Entities.Components;

namespace CasaEngine.Framework.Scene.CharacterMotion;

public struct CharacterMoveToOptions
{
    public float StoppingDistance { get; set; }

    public float TimeoutSeconds { get; set; }

    public CharacterControlMode ControlMode { get; set; }

    public static CharacterMoveToOptions Default => new()
    {
        StoppingDistance = 0.1f,
        TimeoutSeconds = 0f,
        ControlMode = CharacterControlMode.Cutscene,
    };

    public void Validate()
    {
        if (StoppingDistance < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(StoppingDistance));
        }

        if (TimeoutSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds));
        }
    }
}