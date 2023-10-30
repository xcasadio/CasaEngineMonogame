using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Controllers.PlayerState;

public class PlayerDyingState : IState<Controller>
{
    public string Name => "Player Dying State";

    public void Enter(Controller controller)
    {
        var joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);

        var animatedSpriteComponent = controller.Character.Owner.ComponentManager.GetComponent<AnimatedSpriteComponent>();
        animatedSpriteComponent.CreatePhysicsForEachFrame = false;

        controller.Character.SetAnimation(Character.AnimationIndices.Dying, false);
    }

    public void Exit(Controller controller)
    {

    }

    public void Update(Controller controller, float elapsedTime)
    {
    }

    public bool HandleMessage(Controller controller, Message message)
    {
        // end anim 
        //game over ?
        return false;
    }
}