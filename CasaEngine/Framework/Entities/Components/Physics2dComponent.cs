using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using BulletSharp;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logger;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Physics 2d")]
public class Physics2dComponent : Component, ICollideableComponent
{
    public static readonly int ComponentId = (int)ComponentIds.Physics2d;

    private Shape2d _shape;
    private PhysicsEngineComponent _physicsEngineComponent;
    private PhysicsType _physicsType;

    //body
    private Vector2 _velocity;
    private float _mass;
    private float _maxSpeed;
    private float _maxForce;
    private float _maxTurnRate;
    private RigidBody? _rigidBody;

    //static
    private CollisionObject? _collisionObject;

    public HashSet<Collision> Collisions { get; } = new();
    public PhysicsType PhysicsType => PhysicsDefinition.PhysicsType;
    public PhysicsDefinition PhysicsDefinition { get; } = new()
    {
        LinearFactor = new Vector3(1, 1, 0),
        AngularFactor = new Vector3(0, 0, 1)
    };

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

    public Vector2 Velocity
    {
        get => _rigidBody != null ? _rigidBody.LinearVelocity.ToVector2() : Vector2.Zero;
        set
        {
            if (_rigidBody != null)
            {
                _rigidBody.LinearVelocity = new Vector3(value.X, value.Y, 0f);
            }
        }
    }

    public Physics2dComponent(Entity entity) : base(entity, ComponentId)
    {
        _physicsType = PhysicsType.Static;

#if EDITOR
        entity.PositionChanged += OnPositionChanged;
        entity.OrientationChanged += OnOrientationChanged;
#endif
    }

    public override void Initialize(CasaEngineGame game)
    {
        var physicsEngineComponent = game.GetGameComponent<PhysicsEngineComponent>();
        Debug.Assert(physicsEngineComponent != null);

        _physicsEngineComponent = physicsEngineComponent;

#if EDITOR // TODO : usefull ???
        if (_shape == null)
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
#endif

        //TODO : remove this
        if (_shape != null)
        {
            _shape.Position = Owner.Coordinates.LocalPosition.ToVector2();
            //_shape.Rotation = Owner.Coordinates.Rotation;
        }

        var worldMatrix = Matrix.CreateFromQuaternion(Owner.Coordinates.LocalRotation) * Matrix.CreateTranslation(Owner.Coordinates.LocalPosition);

        switch (PhysicsType)
        {
            case PhysicsType.Static:
                _collisionObject = _physicsEngineComponent.AddStaticObject(_shape, ref worldMatrix, this, PhysicsDefinition);
                break;
            case PhysicsType.Kinetic:
                _collisionObject = _physicsEngineComponent.AddGhostObject(_shape, ref worldMatrix, this);
                break;
            default:
                _rigidBody = _physicsEngineComponent.AddRigidBody(_shape, ref worldMatrix, this, PhysicsDefinition);
                break;
        }
    }

    public override void Update(float elapsedTime)
    {
        //In editor mode the game is in idle mode so we don't update physics
#if !EDITOR
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
#endif
    }

    public void OnHit(Collision collision)
    {
        Owner.Hit(collision, this);
    }

    public void OnHitEnded(Collision collision)
    {
        Owner.HitEnded(collision, this);
    }

    public override void Load(JsonElement element)
    {
        _physicsType = element.GetJsonPropertyByName("physics_type").Value.GetEnum<PhysicsType>();

        if (PhysicsType == PhysicsType.Dynamic)
        {
            _mass = element.GetProperty("mass").GetSingle();
        }

        var shapeElement = element.GetProperty("shape");
        if (shapeElement.GetRawText() != "null")
        {
            Shape = ShapeLoader.LoadShape2d(shapeElement);
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        jObject.Add("physics_type", _physicsType.ConvertToString());

        if (PhysicsType == PhysicsType.Dynamic)
        {
            jObject.Add("mass", _mass);
        }

        if (_shape != null)
        {
            JObject newJObject = new();
            _shape.Save(newJObject);
            jObject.Add("shape", newJObject);
        }
        else
        {
            jObject.Add("shape", "null");
        }
    }

    ~Physics2dComponent()
    {
        Owner.PositionChanged -= OnPositionChanged;
        Owner.OrientationChanged -= OnOrientationChanged;
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