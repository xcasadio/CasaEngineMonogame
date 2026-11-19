using System;
using System.ComponentModel;
using System.Diagnostics;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos;

[DisplayName("PlayerComponent")]
public class PlayerComponent : EntityComponent
{
    private CollisionComponent _collisionComponent;
    private AnimatedSpriteComponent _animatedSpriteComponent;

    int index = 0;

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        _collisionComponent = Owner.GetComponent<CollisionComponent>();
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

        // Named actions of the demos' input configuration (DemoInput); nothing is read without a game.
        var game = Owner?.World?.Game;

        if (DemoInput.IsDown(game, DemoInput.MoveDown))
        {
            velocityY = -velocity;
            animationName = "swordman_walk_down";
        }
        else if (DemoInput.IsDown(game, DemoInput.MoveUp))
        {
            velocityY = velocity;
            animationName = "swordman_walk_up";
        }

        if (DemoInput.IsDown(game, DemoInput.MoveRight))
        {
            velocityX = velocity;
            animationName = "swordman_walk_right";
        }
        else if (DemoInput.IsDown(game, DemoInput.MoveLeft))
        {
            velocityX = -velocity;
            animationName = "swordman_walk_left";
        }

        _collisionComponent.Velocity = new Vector3(velocityX, velocityY, 0f);

        if (!string.IsNullOrWhiteSpace(animationName) && !animationName.Equals(_animatedSpriteComponent.CurrentAnimation.Animation2dData.Name))
        {
            Debug.WriteLine(DateTime.Now.TimeOfDay + " " + animationName + " " + _animatedSpriteComponent.CurrentAnimation.Animation2dData.Name);
            _animatedSpriteComponent.SetCurrentAnimation(animationName, true);
        }
        else if (string.IsNullOrWhiteSpace(animationName) && !_animatedSpriteComponent.CurrentAnimation.Animation2dData.Name.Contains("stand"))
        {
            _animatedSpriteComponent.SetCurrentAnimation(_animatedSpriteComponent.CurrentAnimation.Animation2dData.Name.Replace("walk", "stand"), true);
        }

        if (DemoInput.IsDown(game, "TileMap.NextAnimation"))
        {
            index = MathHelper.Min(index + 1, _animatedSpriteComponent.Animations.Count - 1);
            _animatedSpriteComponent.SetCurrentAnimation(index, true);
        }

        if (DemoInput.IsDown(game, "TileMap.PreviousAnimation"))
        {
            index = MathHelper.Max(index - 1, 0);
            _animatedSpriteComponent.SetCurrentAnimation(index, true);
        }
    }
}