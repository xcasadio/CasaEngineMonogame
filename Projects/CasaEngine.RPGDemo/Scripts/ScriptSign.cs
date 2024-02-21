using CasaEngine.Engine.Input;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptSign : GameplayProxy
{
    private InputComponent _inputComponent;

    public override void InitializeWithWorld(World world)
    {
        _inputComponent = world.Game.InputComponent;

        //TODO : do it somewhere else, only fot testing purpose
        var button = new Button();
        button.Name = "Action";
        button.KeyButton = new KeyButton(Keys.Enter);
        button.AlternativeKeyButton = new KeyButton(Buttons.A);
        button.ButtonBehavior = Button.ButtonBehaviors.AnalogInput;
        Button.Buttons.Add(button);
    }

    public override void Update(float elapsedTime)
    {
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
        if (collision.ColliderA.Owner == Owner
            && Owner.World.IsPlayerController(collision.ColliderB.Owner))
        {
            if (_inputComponent.InputBindingsManager.GetInput("Action").IsKeyPressed)
            {
                //create widget
                var playerController = Owner.World.GetPlayerController(collision.ColliderB.Owner);
                playerController.IsInputEnable = false;
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

    public override GameplayProxy Clone()
    {
        return new ScriptSign();
    }
}