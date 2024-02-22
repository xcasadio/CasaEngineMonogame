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
        button.ButtonBehavior = Button.ButtonBehaviors.DigitalInput;
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
        var otherEntity = collision.ColliderA.Owner == Owner ? collision.ColliderB.Owner : collision.ColliderA.Owner;
        var playerController = Owner.World.GetPlayerController(otherEntity);

        if (playerController != null)
        {
            if (_inputComponent.InputBindingsManager.GetInput("Action").IsKeyPressed)
            {
                //create widget
                playerController.IsInputEnable = false;
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

    public override GameplayProxy Clone()
    {
        return new ScriptSign();
    }
}