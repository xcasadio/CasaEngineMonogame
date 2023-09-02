using System;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using RPGDemo.Controllers;

namespace RPGDemo.Weapons;

public class ThrowableWeapon : Weapon
{
    private readonly Physics2dComponent _physics2dComponent;
    private readonly AnimatedSpriteComponent _animatedSpriteComponent;

    public ThrowableWeapon(CasaEngineGame game, Entity entity) : base(game, entity)
    {
        _physics2dComponent = entity.ComponentManager.GetComponent<Physics2dComponent>();
        _animatedSpriteComponent = Entity.ComponentManager.GetComponent<AnimatedSpriteComponent>();
    }

    protected override void Initialize()
    {
    }

    public override void Attach()
    {
        Entity.IsEnabled = true;
        Entity.IsVisible = true;

        var offsetLength = 20f;

        var offset = Character.CurrentDirection switch
        {
            Character2dDirection.Up => Vector2.UnitY * offsetLength,
            Character2dDirection.Down => -Vector2.UnitY * offsetLength,
            Character2dDirection.Left => -Vector2.UnitX * offsetLength,
            Character2dDirection.Right => Vector2.UnitX * offsetLength,
            _ => throw new ArgumentOutOfRangeException()
        };

        _physics2dComponent.SetPosition(Character.Owner.Coordinates.Position + new Vector3(offset, 0f));

        var direction = Character.CurrentDirection switch
        {
            Character2dDirection.Up => Vector2.UnitY,
            Character2dDirection.Down => -Vector2.UnitY,
            Character2dDirection.Left => -Vector2.UnitX,
            Character2dDirection.Right => Vector2.UnitX,
            _ => throw new ArgumentOutOfRangeException()
        };

        _physics2dComponent.Velocity = direction * 200f;
        _animatedSpriteComponent.SetCurrentAnimation("rock", true);

        //spawn
        //SpawnEntity("rock", _game.GameManager.AssetContentManager, _game.GameManager.CurrentWorld);
    }

    private void SpawnEntity(string assetName, AssetContentManager assetContentManager, World world)
    {
        var assetInfo = GameSettings.AssetInfoManager.Get(assetName);
        var entity = assetContentManager.Load<Entity>(assetInfo);
        world.AddEntity(entity);
    }

    public override void UnAttachWeapon()
    {
        Entity.IsEnabled = false;
        Entity.IsVisible = false;

        //_animatedSpriteComponent.RemoveCollisionsFromFrame(_animatedSpriteComponent.CurrentAnimation.CurrentFrame);

        //Entity.Destroy();
    }
}