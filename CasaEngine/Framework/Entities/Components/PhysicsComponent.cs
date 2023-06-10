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

[DisplayName("Physics")]
public class PhysicsComponent : Component, ICollideableComponent
{
    public static readonly int ComponentId = (int)ComponentIds.Physics;
    private Shape3d? _shape;
    private PhysicsEngineComponent _physicsEngineComponent;
    private PhysicsType _physicsType;

    //body
    private Vector3 _velocity;
    private float _mass;
    private float _maxSpeed;
    private float _maxForce;
    private float _maxTurnRate;
    private RigidBody? _rigidBody;

    //static
    private CollisionObject? _collisionObject;

    public PhysicsType PhysicsType
    {
        get => _physicsType;
        set
        {
            _physicsType = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public Vector3 Velocity
    {
        get => _velocity;
        set
        {
            _velocity = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public float Mass
    {
        get => _mass;
        set
        {
            _mass = value;
#if EDITOR
            OnPropertyChanged();
            //TODO : change physics
#endif
        }
    }

    //the maximum speed this entity may travel at.
    public float MaxSpeed
    {
        get => _maxSpeed;
        set
        {
            _maxSpeed = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    //the maximum force this entity can produce to power itself 
    //(think rockets and thrust)
    public float MaxForce
    {
        get => _maxForce;
        set
        {
            _maxForce = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    //the maximum rate (radians per second)this vehicle can rotate         
    public float MaxTurnRate
    {
        get => _maxTurnRate;
        set
        {
            _maxTurnRate = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

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
    }

    public HashSet<Collision> Collisions { get; } = new();

    public PhysicsComponent(Entity entity) : base(entity, ComponentId)
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
            _physicsEngineComponent.RemoveBodyObject(_rigidBody);
            _rigidBody = null;
        }
#endif

        //TODO : remove this
        if (_shape != null)
        {
            _shape.Position = Owner.Coordinates.LocalPosition;
            _shape.Orientation = Owner.Coordinates.Rotation;
        }

        var worldMatrix = Matrix.CreateFromQuaternion(Owner.Coordinates.LocalRotation) * Matrix.CreateTranslation(Owner.Coordinates.LocalPosition);

        switch (PhysicsType)
        {
            case PhysicsType.Static:
                _collisionObject = _physicsEngineComponent.AddStaticObject(_shape, ref worldMatrix, this);
                break;
            case PhysicsType.Kinetic:
                _collisionObject = _physicsEngineComponent.AddGhostObject(_shape, ref worldMatrix, this);
                break;
            default:
                _rigidBody = _physicsEngineComponent.AddRigidBody(_shape, Mass, ref worldMatrix, this);
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
        LogManager.Instance.WriteLineTrace($"Collision : {collision.ColliderA.Owner.Name} & {collision.ColliderB.Owner.Name}");

        //var scriptComponent = Owner.ComponentManager.GetComponent<ScriptComponent>();
        //scriptComponent.TriggerEvent<OnHit>(collision);
    }

    public void OnHitEnded(Collision collision)
    {
        LogManager.Instance.WriteLineTrace($"Collision ended : {collision.ColliderA.Owner.Name} & {collision.ColliderB.Owner.Name}");
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
            Shape = ShapeLoader.LoadShape3d(shapeElement);
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

    ~PhysicsComponent()
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