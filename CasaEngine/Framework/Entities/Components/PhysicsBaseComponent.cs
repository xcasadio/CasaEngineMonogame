using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using BulletSharp;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public abstract class PhysicsBaseComponent : Component, ICollideableComponent
{
    protected PhysicsEngineComponent _physicsEngineComponent;

    //body
    private Vector3 _velocity;
    private float _maxSpeed;
    private float _maxForce;
    private float _maxTurnRate;
    protected RigidBody? _rigidBody;

    //static
    protected CollisionObject? _collisionObject;

    [Browsable(false)]
    public HashSet<Collision> Collisions { get; } = new();

    [Browsable(false)]
    public PhysicsType PhysicsType => PhysicsDefinition.PhysicsType;
    public PhysicsDefinition PhysicsDefinition { get; } = new();

    /*
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
    }*/

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
        PhysicsDefinition.PhysicsType = PhysicsType.Static;
    }

    public override void Initialize(Entity entity, CasaEngineGame game)
    {
        base.Initialize(entity, game);

        _physicsEngineComponent = game.GetGameComponent<PhysicsEngineComponent>();
        Debug.Assert(_physicsEngineComponent != null);

#if EDITOR
        entity.PositionChanged += OnPositionChanged;
        entity.OrientationChanged += OnOrientationChanged;
        DestroyPhysicsObject();
#endif

        CreatePhysicsObject();
    }

    public override void Update(float elapsedTime)
    {
        CollisionObject? collisionObject = null;

        if (_collisionObject != null)
        {
            collisionObject = _collisionObject;
        }
        else if (_rigidBody != null)
        {
            collisionObject = _rigidBody;
        }

        if (collisionObject != null)
        {
            collisionObject.WorldTransform.Decompose(out var scale, out var rotation, out var position);
            Owner.Coordinates.LocalPosition = position;
            Owner.Coordinates.LocalRotation = rotation;
        }
    }

    public override void OnEnabledValueChange()
    {
        if (Owner.IsEnabled)
        {
            CreatePhysicsObject();
        }
        else
        {
            DestroyPhysicsObject();
        }
    }

    private void DestroyPhysicsObject()
    {
        if (_physicsEngineComponent == null)
        {
            return;
        }

        if (_collisionObject != null)
        {
            _physicsEngineComponent.RemoveCollisionObject(_collisionObject);
            _collisionObject = null;
        }

        if (_rigidBody != null)
        {
            _physicsEngineComponent.RemoveRigidBody(_rigidBody);
            _rigidBody = null;
        }

        _physicsEngineComponent.ClearCollisionDataOf(this);
    }

    protected abstract void CreatePhysicsObject();

    public void OnHit(Collision collision)
    {
        Owner.Hit(collision, this);
    }

    public void OnHitEnded(Collision collision)
    {
        Owner.HitEnded(collision, this);
    }
    protected void Clone(PhysicsBaseComponent component)
    {
        component._velocity = _velocity;
        component._maxSpeed = _maxSpeed;
        component._maxForce = _maxForce;
        component._maxTurnRate = _maxTurnRate;
        component.PhysicsDefinition.CopyFrom(PhysicsDefinition);
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        PhysicsDefinition.Load(element.GetProperty("physics_definition"));
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        JObject newJObject = new();
        PhysicsDefinition.Save(newJObject);
        jObject.Add("physics_definition", newJObject);
    }

    ~PhysicsBaseComponent()
    {
        if (Owner != null)
        {
            Owner.PositionChanged -= OnPositionChanged;
            Owner.OrientationChanged -= OnOrientationChanged;
        }
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        if (_collisionObject != null)
        {
            _collisionObject.WorldTransform = Owner.Coordinates.WorldMatrix;
        }
        if (_rigidBody != null)
        {
            _rigidBody.WorldTransform = Owner.Coordinates.WorldMatrix;
        }
    }

    private void OnOrientationChanged(object? sender, EventArgs e)
    {
        if (_collisionObject != null)
        {
            _collisionObject.WorldTransform = Owner.Coordinates.WorldMatrix;
        }
        if (_rigidBody != null)
        {
            _rigidBody.WorldTransform = Owner.Coordinates.WorldMatrix;
        }
    }
#endif
}