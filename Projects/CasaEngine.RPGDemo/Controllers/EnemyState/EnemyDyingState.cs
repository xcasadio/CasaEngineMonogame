﻿using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.RPGDemo.Controllers.EnemyState;

public class EnemyDyingState : IState<Controller>
{
    public string Name => "Enemy Dying State";

    public void Enter(Controller controller)
    {
        controller.Character.Dying();
        var effectEntity = controller.Character.Owner.RootComponent.Owner.World.SpawnEntity<Entity>("smoke_ring_effect");
        effectEntity.Initialize();
        effectEntity.InitializeWithWorld(controller.Character.Owner.World);
        effectEntity.RootComponent.Coordinates.Position = controller.Character.Owner.RootComponent.Position;
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