using CasaEngine.Core.Maths;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class Box2dCollisionComponentViewModel : PhysicsBaseComponentViewModel
{
    private readonly Box2dCollisionComponent _box2dCollisionComponent;
    private RectangleF _rectangle;

    public Box2dCollisionComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _box2dCollisionComponent = (Box2dCollisionComponent)entityComponent;
        _rectangle = new RectangleF(_box2dCollisionComponent.Rectangle.Position.X,
            _box2dCollisionComponent.Rectangle.Position.Y,
            _box2dCollisionComponent.Rectangle.Width,
            _box2dCollisionComponent.Rectangle.Height);
    }

    public RectangleF Rectangle
    {
        get => _rectangle;
        set
        {
            if (_rectangle == value) return;
            var p = _box2dCollisionComponent.Rectangle.Position;
            _box2dCollisionComponent.Rectangle.Position = new Vector2(value.X, value.Y);
            _box2dCollisionComponent.Rectangle.Width = value.Width;
            _box2dCollisionComponent.Rectangle.Height = value.Height;
            OnPropertyChanged();
        }
    }
}