using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos;

[DisplayName("PlayerComponent")]
public class PlayerComponent : ActorComponent
{
    private Physics2dComponent _physics2dComponent;
    private AnimatedSpriteComponent _animatedSpriteComponent;

    int index = 0;

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        _physics2dComponent = Owner.GetComponent<Physics2dComponent>();
        _animatedSpriteComponent = Owner.GetComponent<AnimatedSpriteComponent>();
    }

    public override void InitializeWithWorld(World world)
    {
        base.InitializeWithWorld(world);

        _animatedSpriteComponent.SetCurrentAnimation("swordman_stand_right", true);
    }

    public override PlayerComponent Clone()
    {
        throw new NotImplementedException();
    }

    public override void Update(float elapsedTime)
    {
        var velocity = 80f;
        var velocityX = 0f;
        var velocityY = 0f;
        string animationName = "";

        if (Keyboard.GetState().IsKeyDown(Keys.Down))
        {
            velocityY = -velocity;
            animationName = "swordman_walk_down";
        }
        else if (Keyboard.GetState().IsKeyDown(Keys.Up))
        {
            velocityY = velocity;
            animationName = "swordman_walk_up";
        }

        if (Keyboard.GetState().IsKeyDown(Keys.Right))
        {
            velocityX = velocity;
            animationName = "swordman_walk_right";
        }
        else if (Keyboard.GetState().IsKeyDown(Keys.Left))
        {
            velocityX = -velocity;
            animationName = "swordman_walk_left";
        }

        _physics2dComponent.Velocity = new Vector3(velocityX, velocityY, 0f);

        if (!string.IsNullOrWhiteSpace(animationName) && !animationName.Equals(_animatedSpriteComponent.CurrentAnimation.Animation2dData.Name))
        {
            Debug.WriteLine(DateTime.Now.TimeOfDay + " " + animationName + " " + _animatedSpriteComponent.CurrentAnimation.Animation2dData.Name);
            _animatedSpriteComponent.SetCurrentAnimation(animationName, true);
        }
        else if (string.IsNullOrWhiteSpace(animationName) && !_animatedSpriteComponent.CurrentAnimation.Animation2dData.Name.Contains("stand"))
        {
            _animatedSpriteComponent.SetCurrentAnimation(_animatedSpriteComponent.CurrentAnimation.Animation2dData.Name.Replace("walk", "stand"), true);
        }

        if (Keyboard.GetState().IsKeyDown(Keys.Add))
        {
            index = MathHelper.Min(index + 1, _animatedSpriteComponent.Animations.Count - 1);
            _animatedSpriteComponent.SetCurrentAnimation(index, true);
        }

        if (Keyboard.GetState().IsKeyDown(Keys.Subtract))
        {
            index = MathHelper.Max(index - 1, 0);
            _animatedSpriteComponent.SetCurrentAnimation(index, true);
        }
    }

    public override void Load(JsonElement element)
    {

    }
}