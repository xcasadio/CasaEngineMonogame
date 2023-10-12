using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers;
using CasaEngine.RPGDemo.Controllers.EnemyState;
using CasaEngine.RPGDemo.Controllers.PlayerState;
using CasaEngine.RPGDemo.Weapons;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptPlayer : ExternalComponent, IScriptCharacter
{
    public override int ExternalComponentId => (int)RpgDemoScriptIds.Player;

    private Entity _entity;
    public Character Character { get; private set; }
    public Controller Controller { get; private set; }

    public override void Initialize(Entity entity)
    {
        _entity = entity;
        Character = new Character(_entity, entity.Game);
        Controller = new HumanPlayerController(Character, PlayerIndex.One);

        Character.AnimatationPrefix = "swordman";
        Character.Initialize(entity.Game);
        Controller.Initialize(entity.Game);
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
        var animatedSprite = _entity.ComponentManager.GetComponent<AnimatedSpriteComponent>();
        animatedSprite.SetCurrentAnimation("swordman_stand_right", true);

        //weapon
        var weaponEntity = _entity.Game.GameManager.SpawnEntity("weapon_sword");
        Character.SetWeapon(new MeleeWeapon(_entity.Game, weaponEntity));
        weaponEntity.IsVisible = false;
        weaponEntity.IsEnabled = false;
    }

    private void OnAnimationFinished(object sender, Framework.Assets.Animations.Animation2d animation2d)
    {
        if (sender is Component component)
        {
            Controller.StateMachine.HandleMessage(new Message(component.Owner.Id, component.Owner.Id,
                (int)MessageType.AnimationChanged, 0.0f, animation2d));
        }
    }

    public void Dying()
    {
        Controller.StateMachine.Transition(Controller.GetState((int)PlayerControllerState.Dying));
    }

    public override void Load(JsonElement element, SaveOption option)
    {

    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
    }

#endif
}