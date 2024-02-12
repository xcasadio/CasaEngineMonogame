
using CasaEngine.Core.Serialization;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement.Components;

public abstract class Physics2dComponent : PhysicsBaseComponent
{
    private Shape2d? _shape;

    protected Shape2d? Shape
    {
        get => _shape;
        set
        {
            _shape = value;
            ComputeBoundingBox();

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

        var worldMatrix = WorldMatrixNoScale;

        switch (PhysicsType)
        {
            case PhysicsType.Static:
                _collisionObject = PhysicsEngineComponent.AddStaticObject(_shape, LocalScale, ref worldMatrix, this, PhysicsDefinition);
                break;
            case PhysicsType.Kinetic:
                _collisionObject = PhysicsEngineComponent.AddGhostObject(_shape, LocalScale, ref worldMatrix, this);
                break;
            default:
                _rigidBody = PhysicsEngineComponent.AddRigidBody(_shape, LocalScale, ref worldMatrix, this, PhysicsDefinition);
                break;
        }
    }

    protected override void ComputeBoundingBox()
    {
        _boundingBox = _shape?.BoundingBox ?? new BoundingBox();

        if (Owner != null)
        {
            var min = Vector3.Transform(_boundingBox.Min, WorldMatrixWithScale);
            var max = Vector3.Transform(_boundingBox.Max, WorldMatrixWithScale);
            _boundingBox = new BoundingBox(min, max);
        }
    }

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        ComputeBoundingBox();
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element.TryGetValue("shape", out var shapeElement))
        {
            if (shapeElement.GetString() != "\"null\"")
            {
                var shape = ShapeLoader.LoadShape2d((JObject)shapeElement);

                if (shape is ShapeCircle circle)
                {
                    Scale = new Vector3(circle.Radius);
                }
                else if (shape is ShapeRectangle rectangle)
                {
                    Scale = new Vector3(rectangle.Width, rectangle.Height, 1f);
                }
            }
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        /*
        if (_shape != null)
        {
            var newJObject = new JObject();
            _shape.Save(newJObject);
            jObject.Add("shape", newJObject);
        }
        else
        {
            jObject.Add("shape", "null");
        }*/
    }
#endif
}