using System.Diagnostics;
using System.Text.Json;
using BulletSharp;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement.Components;

public abstract class PhysicsBaseComponent : SceneComponent, ICollideableComponent
{
    protected PhysicsEngineComponent? PhysicsEngineComponent;
    private bool _lock;

    //dynamic
    private Vector3 _velocity;
    private float _maxSpeed;
    private float _maxForce;
    private float _maxTurnRate;
    protected RigidBody? _rigidBody;

    //static
    protected CollisionObject? _collisionObject;

    public HashSet<Collision> Collisions { get; } = new();
    public PhysicsType PhysicsType => PhysicsDefinition.PhysicsType;
    public PhysicsDefinition PhysicsDefinition { get; }

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

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
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

    protected abstract void CreatePhysicsObject();

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

    public override void Load(JsonElement element)
    {
        base.Load(element);
        PhysicsDefinition.Load(element.GetProperty("physics_definition"));
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
            _collisionObject.WorldTransform = WorldMatrixWithScale;
        }

        if (_rigidBody != null)
        {
            _rigidBody.WorldTransform = WorldMatrixWithScale;
        }
    }

    private void OnOrientationChanged(object? sender, EventArgs e)
    {
        if (_collisionObject != null)
        {
            _collisionObject.WorldTransform = WorldMatrixWithScale;
        }

        if (_rigidBody != null)
        {
            _rigidBody.WorldTransform = WorldMatrixWithScale;
        }
    }
#endif
}