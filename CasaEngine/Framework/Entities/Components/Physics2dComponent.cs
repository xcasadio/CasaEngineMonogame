using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Physics 2d")]
public class Physics2dComponent : PhysicsBaseComponent
{
    public static readonly int ComponentId = (int)ComponentIds.Physics2d;

    private Shape2d _shape;

    public Shape2d? Shape
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

    public Physics2dComponent(Entity entity) : base(entity, ComponentId)
    {
        PhysicsDefinition.LinearFactor = new Vector3(1, 1, 0);
        PhysicsDefinition.AngularFactor = new Vector3(0, 0, 1);
    }

    public void SetPosition(Vector3 position)
    {
        var physicObject = _rigidBody ?? _collisionObject;
        var worldMatrix = physicObject.WorldTransform;
        worldMatrix.Translation = position;
        physicObject.WorldTransform = worldMatrix;
    }

    protected override void CreatePhysicsObject()
    {
        if (_physicsEngineComponent == null || _shape == null)
        {
            return;
        }

        _shape.Position = Owner.Coordinates.LocalPosition.ToVector2();
        //_shape.Rotation = Owner.Coordinates.Rotation;

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

    public override Component Clone(Entity owner)
    {
        var component = new Physics2dComponent(owner);
        component._shape = _shape;
        Clone(component);
        return component;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);

        var shapeElement = element.GetProperty("shape");
        if (shapeElement.GetRawText() != "\"null\"")
        {
            Shape = ShapeLoader.LoadShape2d(shapeElement);
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