using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;

namespace CasaEngine.RPGDemo.Weapons;

public class MeleeWeapon : Weapon
{
    private readonly AnimatedSpriteComponent _animatedSpriteComponent;

    public MeleeWeapon(CasaEngineGame game, Entity entity) : base(game, entity)
    {
        _animatedSpriteComponent = Entity.ComponentManager.GetComponent<AnimatedSpriteComponent>();
    }

    protected override void Initialize()
    {
        Entity.Parent = Character.Owner;
    }

    public override void Attach()
    {
        Entity.IsEnabled = true;
        Entity.IsVisible = true;

        var animationName = Character.AnimatedSpriteComponent.CurrentAnimation.AnimationData.AssetInfo.Name;
        var animationNameWithOrientation = animationName.Replace(Character.AnimatationPrefix, "baton");
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