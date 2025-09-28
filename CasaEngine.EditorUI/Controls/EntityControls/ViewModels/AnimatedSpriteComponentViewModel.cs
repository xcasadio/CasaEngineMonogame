using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using CasaEngine.Framework.Assets.Animations;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class AnimatedSpriteComponentViewModel : SceneComponentViewModel
{
    private readonly AnimatedSpriteComponent _animatedSpriteComponent;
    public AnimatedSpriteComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _animatedSpriteComponent = (AnimatedSpriteComponent)entityComponent;
    }

    public Color Color
    {
        get => _animatedSpriteComponent.Color;
        set
        {
            if (_animatedSpriteComponent.Color != value)
            {
                _animatedSpriteComponent.Color = value;
                OnPropertyChanged();
            }
        }
    }

    public SpriteEffects SpriteEffect
    {
        get => _animatedSpriteComponent.SpriteEffect;
        set
        {
            if (_animatedSpriteComponent.SpriteEffect != value)
            {
                _animatedSpriteComponent.SpriteEffect = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CreatePhysicsForEachFrame
    {
        get => _animatedSpriteComponent.CreatePhysicsForEachFrame;
        set
        {
            if (_animatedSpriteComponent.CreatePhysicsForEachFrame != value)
            {
                _animatedSpriteComponent.CreatePhysicsForEachFrame = value;
                OnPropertyChanged();
            }
        }
    }

    public IReadOnlyList<Animation2d> Animations => _animatedSpriteComponent.Animations;

    public Animation2d? CurrentAnimation
    {
        get => _animatedSpriteComponent.CurrentAnimation;
    }

    public Guid CurrentFrameId => _animatedSpriteComponent.GetCurrentFrameName();

    public int CurrentFrameIndex => _animatedSpriteComponent.GetCurrentFrameIndex();

    public void SetCurrentAnimation(Animation2d animation, bool forceReset)
    {
        var previous = _animatedSpriteComponent.CurrentAnimation;
        _animatedSpriteComponent.SetCurrentAnimation(animation, forceReset);
        if (previous != _animatedSpriteComponent.CurrentAnimation)
        {
            OnPropertyChanged(nameof(CurrentAnimation));
            OnPropertyChanged(nameof(CurrentFrameId));
            OnPropertyChanged(nameof(CurrentFrameIndex));
        }
    }

    public void SetCurrentAnimation(int index, bool forceReset)
    {
        var previous = _animatedSpriteComponent.CurrentAnimation;
        _animatedSpriteComponent.SetCurrentAnimation(index, forceReset);
        if (previous != _animatedSpriteComponent.CurrentAnimation)
        {
            OnPropertyChanged(nameof(CurrentAnimation));
            OnPropertyChanged(nameof(CurrentFrameId));
            OnPropertyChanged(nameof(CurrentFrameIndex));
        }
    }

    public bool SetCurrentAnimation(string name, bool forceReset)
    {
        var previous = _animatedSpriteComponent.CurrentAnimation;
        var result = _animatedSpriteComponent.SetCurrentAnimation(name, forceReset);
        if (previous != _animatedSpriteComponent.CurrentAnimation)
        {
            OnPropertyChanged(nameof(CurrentAnimation));
            OnPropertyChanged(nameof(CurrentFrameId));
            OnPropertyChanged(nameof(CurrentFrameIndex));
        }
        return result;
    }

    public void RefreshFrameInfo()
    {
        OnPropertyChanged(nameof(CurrentFrameId));
        OnPropertyChanged(nameof(CurrentFrameIndex));
    }
}