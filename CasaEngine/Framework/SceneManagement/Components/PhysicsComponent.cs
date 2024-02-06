using System.Text.Json;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement.Components;

public abstract class PhysicsComponent : PhysicsBaseComponent
{
    private Shape3d? _shape;
    private BoundingBox _boundingBox;

    protected Shape3d? Shape
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
        //_shape.Orientation = Owner.Coordinates.Orientation;

        var worldMatrix = WorldMatrixNoScale;
        //var worldMatrix = Matrix.CreateFromQuaternion(Orientation) * Matrix.CreateTranslation(Position);

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

    public override void Load(JsonElement element)
    {
        base.Load(element);

        /*var shapeElement = element.GetProperty("shape");
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