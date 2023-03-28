using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using BepuPhysics;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Physics.Shapes;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using Genbox.VelcroPhysics.Dynamics.Solver;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Physics")]
public class PhysicsComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Physics;
    private Shape? _shape;
    private PhysicsEngineComponent _physicsEngineComponent;
    private PhysicsType _physicsType;

    //body
    private Vector3 _velocity;
    private float _mass;
    private float _maxSpeed;
    private float _maxForce;
    private float _maxTurnRate;
    private BodyHandle? _bodyHandle;

    //static
    private StaticHandle? _staticHandle;

    public PhysicsType PhysicsType
    {
        get => _physicsType;
        set
        {
            _physicsType = value;
#if EDITOR
            OnPropertyChanged();
            Initialize();
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

    public Shape? Shape
    {
        get => _shape;
        set
        {
            _shape = value;
#if EDITOR
            OnPropertyChanged();
            Initialize();
#endif
        }
    }

    public PhysicsComponent(Entity entity) : base(entity, ComponentId)
    {
        _physicsType = PhysicsType.Static;
        entity.PositionChanged += OnPositionChanged;
        entity.OrientationChanged += OnOrientationChanged;
    }

    public override void Initialize()
    {
        var physicsEngineComponent = EngineComponents.Game.GetGameComponent<PhysicsEngineComponent>();
        Debug.Assert(physicsEngineComponent != null);

        _physicsEngineComponent = physicsEngineComponent;

#if EDITOR
        if (_shape == null)
        {
            return;
        }

        if (_staticHandle.HasValue)
        {
            _physicsEngineComponent.RemoveStaticObject(_staticHandle.Value);
            _staticHandle = null;
        }
        if (_bodyHandle.HasValue)
        {
            _physicsEngineComponent.RemoveBodyObject(_bodyHandle.Value);
            _bodyHandle = null;
        }
#endif

        if (PhysicsType == PhysicsType.Static)
        {
            _staticHandle = _physicsEngineComponent.AddStaticObject(_shape);
        }
        else
        {
            _bodyHandle = _physicsEngineComponent.AddBodyObject(_shape, Mass);
        }
    }

    public override void Update(float elapsedTime)
    {
        //In editor mode the game is in idle mode so we don't update physics
#if !EDITOR
        if (_bodyHandle != null)
        {
            var transformations = _physicsEngineComponent.GetTransformations(_bodyHandle.Value);
            Owner.Coordinates.LocalPosition = transformations.Item1;
            Owner.Coordinates.LocalRotation = transformations.Item2;
        }
#endif
    }

    public override void Load(JsonElement element)
    {

    }

#if EDITOR
    ~PhysicsComponent()
    {
        Owner.PositionChanged -= OnPositionChanged;
        Owner.OrientationChanged -= OnOrientationChanged;
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        if (PhysicsType == PhysicsType.Static)
        {
            if (_staticHandle.HasValue)
            {
                _physicsEngineComponent.UpdatePositionAndOrientation(Owner.Position, Owner.Orientation, _staticHandle.Value);
            }
        }
        else
        {
            if (_bodyHandle.HasValue)
            {
                _physicsEngineComponent.UpdatePositionAndOrientation(Owner.Position, Owner.Orientation, _bodyHandle.Value);
            }
        }
    }

    private void OnOrientationChanged(object? sender, EventArgs e)
    {
        if (PhysicsType == PhysicsType.Static)
        {
            if (_staticHandle.HasValue)
            {
                _physicsEngineComponent.UpdatePositionAndOrientation(Owner.Position, Owner.Orientation, _staticHandle.Value);
            }
        }
        else
        {
            if (_bodyHandle.HasValue)
            {
                _physicsEngineComponent.UpdatePositionAndOrientation(Owner.Position, Owner.Orientation, _bodyHandle.Value);
            }
        }
    }
#endif
}