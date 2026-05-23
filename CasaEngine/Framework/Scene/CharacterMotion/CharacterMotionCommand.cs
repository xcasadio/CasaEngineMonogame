using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.CharacterMotion;

public readonly struct CharacterMotionCommand
{
    public CharacterMotionCommand(
        CharacterControllerComponent controller,
        Vector2 moveIntent,
        CharacterControlMode controlMode,
        CharacterMotionAuthority authority,
        bool setControlMode = true)
    {
        Controller = controller ?? throw new ArgumentNullException(nameof(controller));
        MoveIntent = moveIntent;
        ControlMode = controlMode;
        Authority = authority;
        SetControlMode = setControlMode;
    }

    public CharacterControllerComponent Controller { get; }

    public Vector2 MoveIntent { get; }

    public CharacterControlMode ControlMode { get; }

    public CharacterMotionAuthority Authority { get; }

    public bool SetControlMode { get; }

    public void Apply()
    {
        if (SetControlMode)
        {
            Controller.SetControlMode(ControlMode);
        }

        Controller.SetMoveIntent(MoveIntent);
    }
}