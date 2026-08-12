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

    //One body per collision profile used by the fixtures of this component.
    protected readonly List<PhysicsBody> _bodies = new();
    private readonly List<FixtureGroup> _fixtureGroups = new();

    public IReadOnlyList<PhysicsBody> Bodies => _bodies;

    public HashSet<Collision> Collisions { get; } = new();
    public PhysicsType PhysicsType => PhysicsDefinition.PhysicsType;
    public PhysicsDefinition PhysicsDefinition { get; }

    public bool SimulatePhysics { get; set; } = true;

    /// <summary>
    /// The single body of a dynamic component, the only one physics may write back to the scene.
    /// </summary>
    protected PhysicsBody DynamicBody => PhysicsType == PhysicsType.Dynamic && _bodies.Count > 0 ? _bodies[0] : null;

    public Vector3 Velocity
    {
        get
        {
            var dynamicBody = DynamicBody;
            if (dynamicBody != null)
            {
                return dynamicBody.LinearVelocity;
            }

            return PhysicsType == PhysicsType.Kinetic ? _velocity : Vector3.Zero;
        }
        set
        {
            var dynamicBody = DynamicBody;
            if (dynamicBody != null)
            {
                dynamicBody.LinearVelocity = value;
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

        //The world constrains its bodies before they are built: the definition is consumed at creation.
        PhysicsWorld?.SpacePolicy?.ApplyDefaultConstraints(PhysicsDefinition);

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

        //Only a dynamic body drives the scene: static and kinetic components are driven by gameplay.
        var dynamicBody = DynamicBody;

        if (dynamicBody != null && Parent != null)
        {
            dynamicBody.WorldTransform.Decompose(out var scale, out var rotation, out var position);
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

        var fixtures = GetFixtures();
        if (fixtures == null || fixtures.Count == 0)
        {
            return;
        }

        BuildFixtureGroups(fixtures);

        if (PhysicsType == PhysicsType.Dynamic && _fixtureGroups.Count > 1)
        {
            int profileCount = _fixtureGroups.Count;
            _fixtureGroups.Clear();
            throw new InvalidOperationException(
                $"A dynamic collision component must use a single collision profile, this one uses {profileCount} of them.");
        }

        var worldMatrix = WorldMatrixNoScale;

        for (int i = 0; i < _fixtureGroups.Count; i++)
        {
            var group = _fixtureGroups[i];
            _bodies.Add(CreateBody(group, ref worldMatrix));
        }
    }

    private PhysicsBody CreateBody(FixtureGroup group, ref Matrix worldMatrix)
    {
        var fixtures = group.Fixtures;
        var singleFixture = fixtures.Count == 1 && fixtures[0].HasIdentityPose ? fixtures[0] : null;

        switch (PhysicsType)
        {
            case PhysicsType.Static:
                return singleFixture != null
                    ? PhysicsWorld.AddStaticObject(singleFixture.Shape, LocalScale, ref worldMatrix, this, PhysicsDefinition, group.ProfileId, singleFixture.Tag)
                    : PhysicsWorld.AddStaticObject(fixtures, LocalScale, ref worldMatrix, this, PhysicsDefinition, group.ProfileId);

            case PhysicsType.Kinetic:
                return singleFixture != null
                    ? PhysicsWorld.AddGhostObject(singleFixture.Shape, LocalScale, ref worldMatrix, this, group.ProfileId, singleFixture.Tag)
                    : PhysicsWorld.AddGhostObject(fixtures, LocalScale, ref worldMatrix, this, group.ProfileId);

            default:
                return singleFixture != null
                    ? PhysicsWorld.AddRigidBody(singleFixture.Shape, LocalScale, ref worldMatrix, this, PhysicsDefinition, group.ProfileId, singleFixture.Tag)
                    : PhysicsWorld.AddRigidBody(fixtures, LocalScale, ref worldMatrix, this, PhysicsDefinition, group.ProfileId);
        }
    }

    /// <summary>
    /// Splits the fixtures per resolved collision profile, keeping the fixture order inside each group.
    /// </summary>
    private void BuildFixtureGroups(IReadOnlyList<ColliderFixture> fixtures)
    {
        _fixtureGroups.Clear();

        var collisionProfiles = GameSettings.PhysicsEngineSettings.CollisionProfiles;
        int componentProfileId = collisionProfiles.ResolveProfileId(PhysicsDefinition);

        for (int i = 0; i < fixtures.Count; i++)
        {
            var fixture = fixtures[i];
            if (fixture.Shape == null)
            {
                throw new InvalidOperationException($"Collider fixture {i} of this collision component has no shape.");
            }

            int profileId = string.IsNullOrEmpty(fixture.ProfileName)
                ? componentProfileId
                : collisionProfiles.GetProfileId(fixture.ProfileName);

            FixtureGroup group = null;
            for (int groupIndex = 0; groupIndex < _fixtureGroups.Count; groupIndex++)
            {
                if (_fixtureGroups[groupIndex].ProfileId == profileId)
                {
                    group = _fixtureGroups[groupIndex];
                    break;
                }
            }

            if (group == null)
            {
                group = new FixtureGroup(profileId);
                _fixtureGroups.Add(group);
            }

            group.Fixtures.Add(fixture);
        }
    }

    protected abstract IReadOnlyList<ColliderFixture> GetFixtures();

    private void DestroyPhysicsObject()
    {
        if (PhysicsWorld == null)
        {
            return;
        }

        for (int i = 0; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            if (body.IsRigidBody)
            {
                PhysicsWorld.RemoveRigidBody(body);
            }
            else
            {
                PhysicsWorld.RemoveCollisionObject(body);
            }

            body.Dispose();
        }

        _bodies.Clear();
        _fixtureGroups.Clear();

        PhysicsWorld.ClearCollisionDataFrom(this);
    }

    public void ApplyImpulse(Vector3 impulse, Vector3 relativePosition)
    {
        //Only a dynamic body answers impulses.
        DynamicBody?.ApplyImpulse(impulse, relativePosition);
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
        if (_bodies.Count == 0)
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
        if (_bodies.Count == 0)
        {
            return;
        }

        Matrix worldTransform = WorldMatrixNoScale;

        for (int i = 0; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            body.WorldTransform = worldTransform;
            PhysicsWorld?.RefreshBodyAabb(body);
        }
    }

    private sealed class FixtureGroup
    {
        public FixtureGroup(int profileId)
        {
            ProfileId = profileId;
        }

        public int ProfileId { get; }

        public List<ColliderFixture> Fixtures { get; } = new();
    }
}