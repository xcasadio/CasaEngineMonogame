using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.RPGDemo.Weapons;

public class MeleeWeapon : Weapon
{
    private AnimatedSpriteComponent _animatedSpriteComponent;

    public MeleeWeapon(CasaEngineGame game, Entity entity) : base(game, entity)
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