using System;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.RPGDemo.Controllers;
using CasaEngine.RPGDemo.Scripts;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Weapons;

public class ThrowableWeapon : Weapon
{
    private readonly string _entityName;

    public ThrowableWeapon(CasaEngineGame game, string entityName) : base(game, null)
    {
        _entityName = entityName;
    }

    protected override void Initialize()
    {
        //Do nothing
    }

    public override void Attach()
    {
        var entity = Game.SpawnEntity(_entityName);
        InitializeEntity(entity);
    }

    private void InitializeEntity(AActor entity)
    {
        //TODO : remove it => must be set by editor
        var scriptEnemyWeapon = new ScriptEnemyWeapon();
        entity.GameplayProxy = scriptEnemyWeapon;

        entity.Initialize();

        var offsetLength = 20f;

        var physics2dComponent = entity.GetComponent<Physics2dComponent>();
        var animatedSpriteComponent = entity.GetComponent<AnimatedSpriteComponent>();

        var offset = Character.CurrentDirection switch
        {
            Character2dDirection.Up => Vector2.UnitY * offsetLength,
            Character2dDirection.Down => -Vector2.UnitY * offsetLength,
            Character2dDirection.Left => -Vector2.UnitX * offsetLength,
            Character2dDirection.Right => Vector2.UnitX * offsetLength,
            _ => throw new ArgumentOutOfRangeException()
        };

        physics2dComponent.Position = Character.Owner.RootComponent.Position + new Vector3(offset, 0f);

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
        //Do nothing
    }
}