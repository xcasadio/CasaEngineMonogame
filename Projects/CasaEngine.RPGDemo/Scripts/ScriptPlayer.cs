using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.GameFramework;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers;
using CasaEngine.RPGDemo.Controllers.PlayerState;
using CasaEngine.RPGDemo.Weapons;
using Microsoft.Xna.Framework;
using Controller = CasaEngine.RPGDemo.Controllers.Controller;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptPlayer : GameplayProxy, IScriptCharacter
{
    public Character Character { get; private set; }
    public Controller Controller { get; private set; }

    public override void InitializeWithWorld(World world)
    {
        Character = new Character((Pawn)Owner);
        Controller = new HumanPlayerController(Character, PlayerIndex.One);

        Character.AnimationPrefix = "swordman";
        Character.Initialize(world);
        Controller.Initialize(world);
        Character.AnimatedSpriteComponent.AnimationFinished += OnAnimationFinished;
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
        var animatedSprite = Owner.GetComponent<AnimatedSpriteComponent>();
        animatedSprite.SetCurrentAnimation("swordman_stand_right", true);

        //weapon
        var weaponEntity = world.SpawnEntity<Entity>("weapon_sword");
        Character.SetWeapon(new MeleeWeapon(world, weaponEntity));
        weaponEntity.IsVisible = false;
        weaponEntity.IsEnabled = false;
    }

    public override void OnEndPlay(World world)
    {

    }

    public override ScriptPlayer Clone()
    {
        return new ScriptPlayer();
    }

    private void OnAnimationFinished(object sender, Framework.Assets.Animations.Animation2d animation2d)
    {
        if (sender is EntityComponent component)
        {
            Controller.StateMachine.HandleMessage(new Message(component.Owner.Id, component.Owner.Id,
                (int)MessageType.AnimationChanged, 0.0f, animation2d));
        }
    }

    public void Dying()
    {
        var physics2dComponent = Character.Owner.GetComponent<Physics2dComponent>();
        physics2dComponent.DisablePhysics();

        Controller.StateMachine.Transition(Controller.GetState((int)PlayerControllerState.Dying));
    }
}