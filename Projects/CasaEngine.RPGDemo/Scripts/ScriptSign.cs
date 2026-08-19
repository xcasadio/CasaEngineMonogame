using CasaEngine.Engine.Input;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptSign : GameplayProxy
{
    private InputComponent _inputComponent;

    public override void InitializeWithWorld(World world)
    {
        _inputComponent = world.Game.InputComponent;

        //TODO : remove do it somewhere else, only fot testing purpose
        var button = new InputMapping();
        button.Name = "Action";
        button.KeyButton = new KeyButton(Keys.Enter);
        button.AlternativeKeyButton = new KeyButton(Buttons.A);
        button.ButtonBehavior = ButtonBehaviors.DigitalInput;
        _inputComponent.InputMappingManager.AddInputMapping(button);

        button = new InputMapping();
        button.Name = "MoveRight";
        button.KeyButton = new KeyButton(Keys.D);
        button.AlternativeKeyButton = new KeyButton(Buttons.LeftThumbstickRight);
        button.ButtonBehavior = ButtonBehaviors.DigitalInput;
        _inputComponent.InputMappingManager.AddInputMapping(button);

        button = new InputMapping();
        button.Name = "MoveLeft";
        button.KeyButton = new KeyButton(Keys.Q);
        button.AlternativeKeyButton = new KeyButton(Buttons.LeftThumbstickLeft);
        button.ButtonBehavior = ButtonBehaviors.DigitalInput;
        _inputComponent.InputMappingManager.AddInputMapping(button);

        button = new InputMapping();
        button.Name = "MoveUp";
        button.KeyButton = new KeyButton(Keys.Z);
        button.AlternativeKeyButton = new KeyButton(Buttons.LeftThumbstickUp);
        button.ButtonBehavior = ButtonBehaviors.DigitalInput;
        _inputComponent.InputMappingManager.AddInputMapping(button);

        button = new InputMapping();
        button.Name = "MoveDown";
        button.KeyButton = new KeyButton(Keys.S);
        button.AlternativeKeyButton = new KeyButton(Buttons.LeftThumbstickDown);
        button.ButtonBehavior = ButtonBehaviors.DigitalInput;
        _inputComponent.InputMappingManager.AddInputMapping(button);
    }

    public override void Update(float elapsedTime)
    {
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
        var otherEntity = collision.ColliderA.Owner == Owner ? collision.ColliderB.Owner : collision.ColliderA.Owner;
        var playerController = Owner.World.GetPlayerController(otherEntity);

        if (playerController != null)
        {
            var actionPressed = playerController.Input != null
                ? playerController.Input.GetButtonState("Action").IsKeyPressed
                : _inputComponent.InputMappingManager.GetButtonState("Action").IsKeyPressed;

            if (actionPressed)
            {
                //create widget
                // TODO: when the sign widget exists, disable input here (playerController.IsInputEnable = false) and re-enable it when the widget closes. Disabling without a widget would freeze the player permanently now that PlayerInput enforces the gate.
                System.Diagnostics.Debug.WriteLine("Read sign");
            }
        }
    }

    public override void OnHitEnded(Collision collision)
    {
    }

    public override void OnBeginPlay(World world)
    {
    }

    public override void OnEndPlay(World world)
    {
    }

    public override IGameplayProxy Clone()
    {
        return new ScriptSign();
    }
}