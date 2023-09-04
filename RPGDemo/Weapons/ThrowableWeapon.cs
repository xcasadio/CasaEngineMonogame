using System;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using RPGDemo.Controllers;
using RPGDemo.Scripts;

namespace RPGDemo.Weapons;

public class ThrowableWeapon : Weapon
{
    public ThrowableWeapon(CasaEngineGame game, Entity entity) : base(game, entity)
    {

    }

    protected override void Initialize()
    {
    }

    public override void Attach()
    {
        //Entity.IsEnabled = true;
        //Entity.IsVisible = true;

        //spawn
        var entity = Game.GameManager.SpawnEntity("weapon_rock");
        InitializeEntity(entity);
    }

    private void InitializeEntity(Entity entity)
    {
        var gamePlayComponent = entity.ComponentManager.GetComponent<GamePlayComponent>();
        entity.ComponentManager.Components.Add(gamePlayComponent);
        gamePlayComponent.ExternalComponent = new ScriptEnemyWeapon(entity);

        var offsetLength = 20f;

        var physics2dComponent = entity.ComponentManager.GetComponent<Physics2dComponent>();
        var animatedSpriteComponent = entity.ComponentManager.GetComponent<AnimatedSpriteComponent>();

        var offset = Character.CurrentDirection switch
        {
            Character2dDirection.Up => Vector2.UnitY * offsetLength,
            Character2dDirection.Down => -Vector2.UnitY * offsetLength,
            Character2dDirection.Left => -Vector2.UnitX * offsetLength,
            Character2dDirection.Right => Vector2.UnitX * offsetLength,
            _ => throw new ArgumentOutOfRangeException()
        };

        physics2dComponent.SetPosition(Character.Owner.Coordinates.Position + new Vector3(offset, 0f));

        var direction = Character.CurrentDirection switch
        {
            Character2dDirection.Up => Vector2.UnitY,
            Character2dDirection.Down => -Vector2.UnitY,
            Character2dDirection.Left => -Vector2.UnitX,
            Character2dDirection.Right => Vector2.UnitX,
            _ => throw new ArgumentOutOfRangeException()
        };

        physics2dComponent.Velocity = direction.ToVector3() * 200f;
        animatedSpriteComponent.SetCurrentAnimation("rock", true);
    }

    public override void UnAttachWeapon()
    {
        //Entity.IsEnabled = false;
        //Entity.IsVisible = false;

        //do nothing
        //Game.GameManager.CurrentWorld.RemoveEntity(Entity);
    }
}