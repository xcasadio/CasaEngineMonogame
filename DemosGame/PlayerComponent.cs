﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using CasaEngine.Core.Shapes;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SharpDX.Direct2D1;
using Component = CasaEngine.Framework.Entities.Component;

namespace DemosGame;

[DisplayName("PlayerComponent")]
public class PlayerComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Custom + 1;

    private Physics2dComponent _physics2dComponent;
    private AnimatedSpriteComponent _animatedSpriteComponent;

    int index = 0;

    public PlayerComponent(Entity owner) : base(owner, ComponentId)
    {
    }

    public override void Initialize(CasaEngineGame game)
    {
        _physics2dComponent = Owner.ComponentManager.GetComponent<Physics2dComponent>();
        _animatedSpriteComponent = Owner.ComponentManager.GetComponent<AnimatedSpriteComponent>();
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

        _physics2dComponent.Velocity = new Vector2(velocityX, velocityY);

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

    public override void Draw()
    {

    }

    public override Component Clone(Entity owner)
    {
        var component = new PlayerComponent(owner);
        return component;
    }

    public override void Load(JsonElement element)
    {

    }
}