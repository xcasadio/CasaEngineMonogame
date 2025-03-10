﻿using System.Linq;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.GameFramework;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers;
using CasaEngine.RPGDemo.Controllers.EnemyState;
using CasaEngine.RPGDemo.Weapons;
using Controller = CasaEngine.RPGDemo.Controllers.Controller;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptEnemy : GameplayProxy, IScriptCharacter
{
    public Character Character { get; private set; }
    public Controller Controller { get; private set; }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        Character = new Character((Pawn)Owner);
        Controller = new EnemyController(Character);
    }

    public override void InitializeWithWorld(World world)
    {
        Character.Initialize(world);
        Controller.Initialize(world);

        Character.AnimationPrefix = "octopus";
        Character.AnimatedSpriteComponent.AnimationFinished += OnAnimationFinished;

        var animatedSprite = Owner.GetComponent<AnimatedSpriteComponent>();
        animatedSprite.SetCurrentAnimation("octopus_stand_right", true);

        Character.SetWeapon(new ThrowableWeapon(world, "weapon_rock"));

        var entity = world.Game.GameManager.CurrentWorld.Entities.First(x => x.Name == "character_link");
        (Controller as EnemyController).PlayerHunted = (entity.GameplayProxy as IScriptCharacter).Character;
    }

    public override void Update(float elapsedTime)
    {
        Controller.Update(elapsedTime);
        Character.Update(elapsedTime);
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
        //Controller.OnHit(collision);
    }

    public override void OnHitEnded(Collision collision)
    {
        //Controller.OnHitEnded(collision);
    }

    public override void OnBeginPlay(World world)
    {
    }

    public override void OnEndPlay(World world)
    {

    }

    public override ScriptEnemy Clone()
    {
        return new ScriptEnemy();
    }

    private void OnAnimationFinished(object sender, CasaEngine.Framework.Assets.Animations.Animation2d animation2d)
    {
        if (sender is EntityComponent component)
        {
            Controller.StateMachine.HandleMessage(new Message(component.Owner.Id, component.Owner.Id,
                (int)MessageType.AnimationChanged, 0.0f, animation2d));
        }
    }

    public void Dying()
    {
        Controller.StateMachine.Transition(Controller.GetState((int)EnemyControllerState.Dying));
    }
}