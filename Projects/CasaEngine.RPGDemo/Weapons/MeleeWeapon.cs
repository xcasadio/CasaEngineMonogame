using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;

namespace CasaEngine.RPGDemo.Weapons;

public class MeleeWeapon : Weapon
{
    private AnimatedSpriteComponent _animatedSpriteComponent;

    public MeleeWeapon(World world, Entity entity) : base(world, entity)
    {
    }

    protected override void Initialize()
    {
        Character.Owner.AddChild(Entity);
        _animatedSpriteComponent = Entity.GetComponent<AnimatedSpriteComponent>();
    }

    public override void Attach()
    {
        Entity.IsEnabled = true;
        Entity.IsVisible = true;

        var animationName = Character.AnimatedSpriteComponent.CurrentAnimation.AnimationData.Name;
        var animationNameWithOrientation = animationName.Replace(Character.AnimationPrefix, "baton");
        _animatedSpriteComponent.SetCurrentAnimation(animationNameWithOrientation, true);
    }

    public override void UnAttachWeapon()
    {
        Entity.IsEnabled = false;
        Entity.IsVisible = false;

        //collision from the last frame is not removed !!
        //_animatedSpriteComponent.RemoveCollisionsFromFrame(_animatedSpriteComponent.CurrentAnimation.CurrentFrame);
    }
}