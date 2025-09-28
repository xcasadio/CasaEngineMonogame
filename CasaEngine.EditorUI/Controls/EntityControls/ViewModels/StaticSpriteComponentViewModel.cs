using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class StaticSpriteComponentViewModel : SceneComponentViewModel
{
    private readonly StaticSpriteComponent _staticSpriteComponent;
    public StaticSpriteComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _staticSpriteComponent = (StaticSpriteComponent)entityComponent;
    }

    public Color Color
    {
        get => _staticSpriteComponent.Color;
        set
        {
            if (_staticSpriteComponent.Color != value)
            {
                _staticSpriteComponent.Color = value;
                OnPropertyChanged();
            }
        }
    }

    public SpriteEffects SpriteEffect
    {
        get => _staticSpriteComponent.SpriteEffect;
        set
        {
            if (_staticSpriteComponent.SpriteEffect != value)
            {
                _staticSpriteComponent.SpriteEffect = value;
                OnPropertyChanged();
            }
        }
    }
}