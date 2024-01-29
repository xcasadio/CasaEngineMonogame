using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement.Components;

public abstract class Physics2dComponent : PhysicsBaseComponent
{
    private Shape2d? _shape;
    private BoundingBox _boundingBox;

    protected Shape2d? Shape
    {
        get => _shape;
        set
        {
            _shape = value;
            _boundingBox = _shape?.BoundingBox ?? new BoundingBox();

#if EDITOR
            ReCreatePhysicsObject();
#endif
        }
    }

    public Physics2dComponent()
    {
        PhysicsDefinition.LinearFactor = new Vector3(1, 1, 0);
        PhysicsDefinition.AngularFactor = new Vector3(0, 0, 1);
    }

    public Physics2dComponent(Physics2dComponent other) : base(other)
    {
        _shape = other._shape;
    }

    protected override void CreatePhysicsObject()
    {
        if (PhysicsEngineComponent == null || _shape == null)
        {
            return;
        }

        _shape.Position = Position.ToVector2();
        //_shape.Orientation = Owner.Coordinates.Orientation;

        var worldMatrix = WorldMatrixNoScale;
        //var worldMatrix = Matrix.CreateFromQuaternion(Orientation) * Matrix.CreateTranslation(Position);

        switch (PhysicsType)
        {
            case PhysicsType.Static:
                _collisionObject =
                    PhysicsEngineComponent.AddStaticObject(_shape, LocalScale, ref worldMatrix, this, PhysicsDefinition);
                break;
            case PhysicsType.Kinetic:
                _collisionObject = PhysicsEngineComponent.AddGhostObject(_shape, LocalScale, ref worldMatrix, this);
                break;
            default:
                _rigidBody = PhysicsEngineComponent.AddRigidBody(_shape, LocalScale, ref worldMatrix, this, PhysicsDefinition);
                break;
        }
    }

    public override BoundingBox GetBoundingBox()
    {
        return _boundingBox;
    }

    public override void Load(JsonElement element)
    {
        base.Load(element);

        var shapeElement = element.GetProperty("shape");
        if (shapeElement.GetRawText() != "\"null\"")
        {
            Shape = ShapeLoader.LoadShape2d(shapeElement);
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

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