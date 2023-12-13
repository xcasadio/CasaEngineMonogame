using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Physics")]
public class PhysicsComponent : PhysicsBaseComponent
{
    public override int ComponentId => (int)ComponentIds.Physics;
    private Shape3d? _shape;

    public Shape3d? Shape
    {
        get => _shape;
        set
        {
            _shape = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    protected override void CreatePhysicsObject()
    {
        if (_physicsEngineComponent == null || _shape == null)
        {
            return;
        }

        _shape.Position = Owner.Coordinates.LocalPosition;
        _shape.Orientation = Owner.Coordinates.Rotation;

        var worldMatrix = Matrix.CreateFromQuaternion(Owner.Coordinates.LocalRotation) *
                          Matrix.CreateTranslation(Owner.Coordinates.LocalPosition);

        switch (PhysicsType)
        {
            case PhysicsType.Static:
                _collisionObject =
                    _physicsEngineComponent.AddStaticObject(_shape, ref worldMatrix, this, PhysicsDefinition);
                break;
            case PhysicsType.Kinetic:
                _collisionObject = _physicsEngineComponent.AddGhostObject(_shape, ref worldMatrix, this);
                break;
            default:
                _rigidBody = _physicsEngineComponent.AddRigidBody(_shape, ref worldMatrix, this, PhysicsDefinition);
                break;
        }
    }

    public override Component Clone()
    {
        var component = new PhysicsComponent();
        component._shape = _shape;
        Clone(component);
        return component;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);

        var shapeElement = element.GetProperty("shape");
        if (shapeElement.GetRawText() != "null")
        {
            Shape = ShapeLoader.LoadShape3d(shapeElement);
        }
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        if (_shape != null)
        {
            var newJObject = new JObject();
            _shape.Save(newJObject);
            jObject.Add("shape", newJObject);
        }
        else
        {
            jObject.Add("shape", "null");
        }
    }

#endif
}