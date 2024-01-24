using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.RPGDemo.Controllers.EnemyState;

public class EnemyDyingState : IState<Controller>
{
    public string Name => "Enemy Dying State";

    public void Enter(Controller controller)
    {
        controller.Character.Dying();
        var effectEntity = controller.Character.Owner.RootComponent.World.Game.SpawnEntity("smoke_ring_effect");
        effectEntity.RootComponent.Coordinates.Position = controller.Character.Owner.RootComponent.WorldPosition;
        var animatedSpriteComponent = effectEntity.GetComponent<AnimatedSpriteComponent>();
        animatedSpriteComponent.SetCurrentAnimation(0, true);

        animatedSpriteComponent.AnimationFinished += OnAnimationFinished;
    }

    private void OnAnimationFinished(object sender, Framework.Assets.Animations.Animation2d e)
    {
        var animatedSpriteComponent = sender as AnimatedSpriteComponent;
        animatedSpriteComponent.Owner.Destroy();
    }

    public void Exit(Controller controller)
    {

    }

    public void Update(Controller controller, float elapsedTime)
    {
    }

    public bool HandleMessage(Controller controller, Message message)
    {
        return false;
    }
}