using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public abstract class PhysicsComponent : PhysicsBaseComponent
{
    private Shape3d? _shape;
    private BoundingBox _boundingBox;

    protected Shape3d? Shape
    {
        get => _shape;
        set
        {
            _shape = value;
            ComputeBoundingBox();

#if EDITOR
            ReCreatePhysicsObject();
            //OnPropertyChanged();
#endif
        }
    }

    protected PhysicsComponent()
    {

    }

    protected PhysicsComponent(PhysicsComponent other) : base(other)
    {
        _shape = other._shape;
    }

    protected override void CreatePhysicsObject()
    {
        if (PhysicsEngineComponent == null || _shape == null || !SimulatePhysics)
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

        /*var shapeElement = element["shape");
        if (shapeElement.GetRawText() != "null")
        {
            Shape = ShapeLoader.LoadShape3d(shapeElement);
        }*/
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