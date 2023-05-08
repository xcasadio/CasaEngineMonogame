using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using BepuPhysics;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Physics")]
public class PhysicsComponent : Component
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

        //TODO : remove this
        if (_shape != null)
        {
            _shape.Position = Owner.Coordinates.LocalPosition;
            _shape.Orientation = Owner.Coordinates.Rotation;
        }

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
            Owner.Coordinates.LocalPosition = transformations.Position;
            Owner.Coordinates.LocalRotation = transformations.Orientation;
        }
#endif
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