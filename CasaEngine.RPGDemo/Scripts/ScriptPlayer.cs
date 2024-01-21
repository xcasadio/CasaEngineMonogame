using System.Text.Json;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers;
using CasaEngine.RPGDemo.Controllers.PlayerState;
using CasaEngine.RPGDemo.Weapons;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptPlayer : GameplayProxy, IScriptCharacter
{
    public Character Character { get; private set; }
    public Controller Controller { get; private set; }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public override void InitializeWithWorld(World world)
    {
        Character = new Character(Owner);
        Controller = new HumanPlayerController(Character, PlayerIndex.One);

        Character.AnimatationPrefix = "swordman";
        Character.Initialize(world.Game);
        Controller.Initialize(world.Game);
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
        var weaponEntity = world.Game.GameManager.SpawnEntity("weapon_sword");
        Character.SetWeapon(new MeleeWeapon(world.Game, weaponEntity));
        weaponEntity.IsVisible = false;
        weaponEntity.IsEnabled = false;
    }

    public override void OnEndPlay(World world)
    {

    }

    private void OnAnimationFinished(object sender, Framework.Assets.Animations.Animation2d animation2d)
    {
        if (sender is ActorComponent component)
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

    public override void Load(JsonElement element)
    {

    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
    }

#endif
}