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
            if (_shape != null)
            {
                _shape.PropertyChanged -= OnPropertyChanged;
            }

            _shape = value;

            if (_shape != null)
            {
                _shape.PropertyChanged += OnPropertyChanged;
            }

#if EDITOR
            ReCreatePhysicsObject();
            OnPropertyChanged();
#endif
        }
    }

    private void OnPropertyChanged(object? sender, string e)
    {
        ReCreatePhysicsObject();
    }

    protected override void CreatePhysicsObject()
    {
        if (PhysicsEngineComponent == null || _shape == null)
        {
            return;
        }

        //TODO : _shape.Position & _shape.Orientation must be used
        //in the worldmatrix or in the creation of the shape ?
        //_shape.Position = Owner.Coordinates.LocalPosition;
        //_shape.Orientation = Owner.Coordinates.Rotation;
        var worldMatrix = Matrix.CreateFromQuaternion(Owner.Coordinates.LocalRotation) *
                          Matrix.CreateTranslation(Owner.Coordinates.LocalPosition);

        switch (PhysicsType)
        {
            case PhysicsType.Static:
                _collisionObject =
                    PhysicsEngineComponent.AddStaticObject(_shape, ref worldMatrix, this, PhysicsDefinition);
                break;
            case PhysicsType.Kinetic:
                _collisionObject = PhysicsEngineComponent.AddGhostObject(_shape, ref worldMatrix, this);
                break;
            default:
                _rigidBody = PhysicsEngineComponent.AddRigidBody(_shape, ref worldMatrix, this, PhysicsDefinition);
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