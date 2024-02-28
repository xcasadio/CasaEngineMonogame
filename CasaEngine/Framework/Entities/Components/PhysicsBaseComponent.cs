using System.Diagnostics;
using BulletSharp;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public abstract class PhysicsBaseComponent : SceneComponent, ICollideableComponent
{
    protected PhysicsEngineComponent? PhysicsEngineComponent;
    private BoundingBox _boundingBox;
    private bool _lock;

    //dynamic object
    private Vector3 _velocity;
    private float _maxSpeed;
    private float _maxForce;
    private float _maxTurnRate;
    protected RigidBody? _rigidBody;

    //static object
    protected CollisionObject? _collisionObject;

    public HashSet<Collision> Collisions { get; } = new();
    public PhysicsType PhysicsType => PhysicsDefinition.PhysicsType;
    public PhysicsDefinition PhysicsDefinition { get; }

    public bool SimulatePhysics { get; set; } = true;

    public Vector3 Velocity
    {
        get => _rigidBody?.LinearVelocity ?? Vector3.Zero;
        set
        {
            if (_rigidBody != null)
            {
                _rigidBody.LinearVelocity = value;
            }
        }
    }

    protected PhysicsBaseComponent()
    {
        PhysicsDefinition = new();
        PhysicsDefinition.PhysicsType = PhysicsType.Static;
    }

    protected PhysicsBaseComponent(PhysicsBaseComponent other) : base(other)
    {
        _velocity = other._velocity;
        _maxSpeed = other._maxSpeed;
        _maxForce = other._maxForce;
        _maxTurnRate = other._maxTurnRate;
        PhysicsDefinition = new(other.PhysicsDefinition);
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        PhysicsEngineComponent = world.Game.GetGameComponent<PhysicsEngineComponent>();
        Debug.Assert(PhysicsEngineComponent != null);

#if EDITOR
        Coordinates.PositionChanged += OnPositionChanged;
        Coordinates.OrientationChanged += OnOrientationChanged;
        DestroyPhysicsObject();
#endif

        CreatePhysicsObject();
    }

    protected abstract BoundingBox ComputeBoundingBox();

    public override BoundingBox GetBoundingBox()
    {
        if (IsBoundingBoxDirty)
        {
            _boundingBox = ComputeBoundingBox();

            if (Owner != null)
            {
                var min = Vector3.Transform(_boundingBox.Min, WorldMatrixWithScale);
                var max = Vector3.Transform(_boundingBox.Max, WorldMatrixWithScale);
                _boundingBox = new BoundingBox(min, max);
            }

            IsBoundingBoxDirty = false;
        }

        return _boundingBox;
    }

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        ComputeBoundingBox();
    }

    public override void Detach()
    {
        DestroyPhysicsObject();
    }

    public override void Update(float elapsedTime)
    {
#if EDITOR
        if (Owner.World.Game.IsRunningInGameEditorMode)
        {
            return;
        }
#endif

        CollisionObject? collisionObject = _collisionObject ?? _rigidBody;

        if (collisionObject != null && Parent != null)
        {
            collisionObject.WorldTransform.Decompose(out var scale, out var rotation, out var position);
            //Set only the owner
            //Test how to set all the hierarchy, but how we do with several physic component ?
            //TODO bug : use localMatrix + Actor matrix to calculated the right position of the root component
            Parent.Coordinates.Position = position;
            Parent.Coordinates.Orientation = rotation;
        }
    }

    public override void OnEnabledValueChange()
    {
        base.OnEnabledValueChange();

        if (Owner == null)
        {
            return;
        }

        if (Owner.IsEnabled)
        {
            CreatePhysicsObject();
        }
        else
        {
            DestroyPhysicsObject();
        }
    }

    public void DisablePhysics()
    {
        DestroyPhysicsObject();
    }

    private void CreatePhysicsObject()
    {
        if (PhysicsEngineComponent == null || !SimulatePhysics)
        {
            return;
        }

        var worldMatrix = WorldMatrixNoScale;

        var collisionShape = ConvertToCollisionShape();
        collisionShape.LocalScaling = LocalScale;
        collisionShape.UserObject = this;

        switch (PhysicsType)
        {
            case PhysicsType.Static:
                _collisionObject = PhysicsEngineComponent.AddStaticObject(collisionShape, LocalScale, ref worldMatrix, this, PhysicsDefinition);
                break;
            case PhysicsType.Kinetic:
                _collisionObject = PhysicsEngineComponent.AddGhostObject(collisionShape, ref worldMatrix, this);
                break;
            default:
                _rigidBody = PhysicsEngineComponent.AddRigidBody(collisionShape, LocalScale, ref worldMatrix, this, PhysicsDefinition);
                break;
        }
    }

    protected abstract CollisionShape ConvertToCollisionShape();

    private void DestroyPhysicsObject()
    {
        if (PhysicsEngineComponent == null)
        {
            return;
        }

        if (_collisionObject != null)
        {
            PhysicsEngineComponent.RemoveCollisionObject(_collisionObject);
            _collisionObject = null;
        }

        if (_rigidBody != null)
        {
            PhysicsEngineComponent.RemoveRigidBody(_rigidBody);
            _rigidBody = null;
        }

        PhysicsEngineComponent.ClearCollisionDataFrom(this);
    }

    public void ApplyImpulse(Vector3 impulse, Vector3 relativePosition)
    {
        //do nothing with _collisionObject
        _rigidBody?.ApplyImpulse(impulse, relativePosition);
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        PhysicsDefinition.Load((JObject)element["physics_definition"]);
    }

    protected void ReCreatePhysicsObject()
    {
        if (_lock)
        {
            return;
        }

        _lock = true;

        DestroyPhysicsObject();
        CreatePhysicsObject();

        _lock = false;
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        PhysicsDefinition.Save(newJObject);
        jObject.Add("physics_definition", newJObject);
    }

    ~PhysicsBaseComponent()
    {
        if (Owner != null)
        {
            Coordinates.PositionChanged -= OnPositionChanged;
            Coordinates.OrientationChanged -= OnOrientationChanged;
        }
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        if (_collisionObject != null)
        {
            _collisionObject.WorldTransform = WorldMatrixNoScale;
        }

        if (_rigidBody != null)
        {
            _rigidBody.WorldTransform = WorldMatrixNoScale;
        }

        IsBoundingBoxDirty = true;
    }

    private void OnOrientationChanged(object? sender, EventArgs e)
    {
        if (_collisionObject != null)
        {
            _collisionObject.WorldTransform = WorldMatrixNoScale;
        }

        if (_rigidBody != null)
        {
            _rigidBody.WorldTransform = WorldMatrixNoScale;
        }

        IsBoundingBoxDirty = true;
    }
#endif
}