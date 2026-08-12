using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

public abstract class PhysicsBaseComponent : SceneComponent, ICollideableComponent
{
    protected IPhysicsWorld PhysicsWorld;
    private BoundingBox _boundingBox;
    private bool _lock;

    //dynamic object
    private Vector3 _velocity;
    private float _maxSpeed;
    private float _maxForce;
    private float _maxTurnRate;
    protected PhysicsBody _rigidBody;

    //static object
    protected PhysicsBody _collisionObject;

    public HashSet<Collision> Collisions { get; } = new();
    public PhysicsType PhysicsType => PhysicsDefinition.PhysicsType;
    public PhysicsDefinition PhysicsDefinition { get; }

    public bool SimulatePhysics { get; set; } = true;

    public Vector3 Velocity
    {
        get
        {
            if (_rigidBody != null)
            {
                return _rigidBody.LinearVelocity;
            }

            return PhysicsType == PhysicsType.Kinetic ? _velocity : Vector3.Zero;
        }
        set
        {
            if (_rigidBody != null)
            {
                _rigidBody.LinearVelocity = value;
                return;
            }

            if (PhysicsType == PhysicsType.Kinetic)
            {
                _velocity = value;
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

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        PhysicsWorld = world.PhysicsWorld;
        System.Diagnostics.Debug.Assert(PhysicsWorld != null);

    if (world.Game.ExecutionPolicy.UseExternalViewManagement)
    {
        LocalTransform.PositionChanged += OnPositionChanged;
        LocalTransform.OrientationChanged += OnOrientationChanged;
        DestroyPhysicsObject();
    }

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
                _boundingBox = _boundingBox.Transform(WorldMatrixWithScale);
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
        base.Update(elapsedTime);

        if (!Owner.World.Game.ExecutionPolicy.UpdatePhysicsComponents)
        {
            if (Owner.World.Game.ExecutionPolicy.UseExternalViewManagement && IsBoundingBoxDirty)
            {
                ApplyPhysicsWorldTransform();
            }

            return;
        }

        if (PhysicsType == PhysicsType.Kinetic)
        {
            SyncTransformFromScene();
            return;
        }

        PhysicsBody collisionObject = _collisionObject ?? _rigidBody;

        if (collisionObject != null && Parent != null)
        {
            collisionObject.WorldTransform.Decompose(out var scale, out var rotation, out var position);
            //Set only the owner
            //Test how to set all the hierarchy, but how we do with several physic component ?
            //TODO bug : use localMatrix + Actor matrix to calculated the right position of the root component
            Parent.LocalTransform.Position = position;
            Parent.LocalTransform.Orientation = rotation;
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
        if (PhysicsWorld == null || !SimulatePhysics)
        {
            return;
        }

        var worldMatrix = WorldMatrixNoScale;

        var collisionShape = ConvertToCollisionShape();
        collisionShape.LocalScaling = LocalScale;

        switch (PhysicsType)
        {
            case PhysicsType.Static:
                _collisionObject = PhysicsWorld.AddStaticObject(collisionShape, LocalScale, ref worldMatrix, this, PhysicsDefinition);
                break;
            case PhysicsType.Kinetic:
                var collisionProfileId = GameSettings.PhysicsEngineSettings.CollisionProfiles.ResolveProfileId(PhysicsDefinition);
                _collisionObject = PhysicsWorld.AddGhostObject(collisionShape, ref worldMatrix, this, collisionProfileId);
                break;
            default:
                _rigidBody = PhysicsWorld.AddRigidBody(collisionShape, LocalScale, ref worldMatrix, this, PhysicsDefinition);
                break;
        }
    }

    protected abstract PhysicsShape ConvertToCollisionShape();

    private void DestroyPhysicsObject()
    {
        if (PhysicsWorld == null)
        {
            return;
        }

        if (_collisionObject != null)
        {
            PhysicsWorld.RemoveCollisionObject(_collisionObject);
            _collisionObject.Dispose();
            _collisionObject = null;
        }

        if (_rigidBody != null)
        {
            PhysicsWorld.RemoveRigidBody(_rigidBody);
            _rigidBody.Dispose();
            _rigidBody = null;
        }

        PhysicsWorld.ClearCollisionDataFrom(this);
    }

    public void ApplyImpulse(Vector3 impulse, Vector3 relativePosition)
    {
        //do nothing with _collisionObject
        _rigidBody?.ApplyImpulse(impulse, relativePosition);
    }

    public void AdvanceKinematic(float elapsedTime)
    {
        if (PhysicsType != PhysicsType.Kinetic || Parent == null)
        {
            return;
        }

        Parent.LocalTransform.Position += _velocity * elapsedTime;
        SyncTransformFromScene();
    }

    public void SyncTransformFromScene()
    {
        if (_collisionObject == null && _rigidBody == null)
        {
            return;
        }

        ApplyPhysicsWorldTransform();
        IsBoundingBoxDirty = true;
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

    ~PhysicsBaseComponent()
    {
        if (Owner != null)
        {
            LocalTransform.PositionChanged -= OnPositionChanged;
            LocalTransform.OrientationChanged -= OnOrientationChanged;
        }
    }

    private void OnPositionChanged(object sender, EventArgs e)
    {
        ApplyPhysicsWorldTransform();
        IsBoundingBoxDirty = true;
    }

    private void OnOrientationChanged(object sender, EventArgs e)
    {
        ApplyPhysicsWorldTransform();
        IsBoundingBoxDirty = true;
    }

    private void ApplyPhysicsWorldTransform()
    {
        if (_collisionObject == null && _rigidBody == null)
        {
            return;
        }

        Matrix worldTransform = WorldMatrixNoScale;

        if (_collisionObject != null)
        {
            _collisionObject.WorldTransform = worldTransform;
            PhysicsWorld?.RefreshBodyAabb(_collisionObject);
        }

        if (_rigidBody != null)
        {
            _rigidBody.WorldTransform = worldTransform;
            PhysicsWorld?.RefreshBodyAabb(_rigidBody);
        }
    }
}