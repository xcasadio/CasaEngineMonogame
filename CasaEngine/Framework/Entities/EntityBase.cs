using System.Text.Json;
using CasaEngine.Core.Logs;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;

namespace CasaEngine.Framework.Entities;

public class EntityBase : Asset
#if EDITOR
    , ITransformable
#endif
{
    private bool _isEnabled = true;

    public event EventHandler? BeginPlay;
    public event EventHandler? EndPlay;
    public event EventHandler? Destroyed;
    public event EventHandler<float>? Tick;
    public event EventHandler? OnReset;
    //public event EventHandler<EventCollisionArgs>? Damaged;
    //public event EventHandler<EventCollisionArgs>? Hit;
    //public event EventHandler<EventCollisionArgs>? BeginOverlap;
    //public event EventHandler<EventCollisionArgs>? EndOverlap;
    public event EventHandler<EventCollisionArgs>? OnHit;
    public event EventHandler<EventCollisionArgs>? OnHitEnded;

    public CasaEngineGame Game { get; private set; }

    public long Id => AssetInfo.Id;

    public string Name
    {
        get => AssetInfo.Name;
        set => AssetInfo.Name = value;
    }

    public Coordinates Coordinates { get; } = new();

    public List<EntityBase> Children { get; } = new();

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            //LogManager.Instance.WriteTrace($"Entity {Name} is {(_isEnabled ? "enabled" : "disabled")}");
            OnEnabledValueChange();
        }
    }

    public bool IsVisible { get; set; } = true;

    public bool ToBeRemoved { get; private set; }

    public bool IsTemporary { get; internal set; }

    public virtual void Initialize(CasaEngineGame game)
    {
        Game = game;
    }

    public void Update(float elapsedTime)
    {
        if (IsEnabled == false)
        {
            return;
        }

        UpdateInternal(elapsedTime);

        Tick?.Invoke(this, elapsedTime);
    }

    protected virtual void UpdateInternal(float elapsedTime)
    {
    }

    public void Draw()
    {
        if (IsVisible == false)
        {
            return;
        }

        DrawInternal();
    }

    protected virtual void DrawInternal()
    {
    }

    public EntityBase Clone()
    {
        var entity = new EntityBase();
        entity.CopyFrom(this);
        return entity;
    }

    public void CopyFrom(EntityBase entityBase)
    {
        Coordinates.CopyFrom(entityBase.Coordinates);

        IsTemporary = entityBase.IsTemporary;
        _isEnabled = entityBase._isEnabled;
        IsVisible = entityBase.IsVisible;
    }

    public void Destroy()
    {
        ToBeRemoved = true;
        IsEnabled = false;
        IsVisible = false;

        OnDestroyed();
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element.GetProperty("asset"), option);

        //var version = element.GetJsonPropertyByName("version").Value.GetInt32();

        var jsonCoordinate = element.GetJsonPropertyByName("coordinates").Value;
        Coordinates.Load(jsonCoordinate);
    }

    public virtual void ScreenResized(int width, int height)
    {
    }

    protected virtual void OnEnabledValueChange()
    {
    }

    public virtual void Hit(Collision collision, Component component)
    {
        //LogManager.Instance.WriteTrace($"OnHit : {collision.ColliderA.Owner.Name} & {collision.ColliderB.Owner.Name}");
        OnHit?.Invoke(this, new EventCollisionArgs(collision, component));
    }

    public virtual void HitEnded(Collision collision, Component component)
    {
        //LogManager.Instance.WriteTrace($"OnHitEnded : {collision.ColliderA.Owner.Name} & {collision.ColliderB.Owner.Name}");
        OnHitEnded?.Invoke(this, new EventCollisionArgs(collision, component));
    }

    public virtual void OnBeginPlay(World.World world)
    {
        BeginPlay?.Invoke(this, EventArgs.Empty);
    }

    public virtual void OnEndPlay(World.World world)
    {
        EndPlay?.Invoke(this, EventArgs.Empty);
    }

    public void OnEndPlay()
    {
        EndPlay?.Invoke(this, EventArgs.Empty);
    }

    public void OnDestroyed()
    {
        Destroyed?.Invoke(this, EventArgs.Empty);
    }

    public virtual void Accept(CullVisitor cullVisitor)
    {
        cullVisitor.Apply(this);
    }

#if EDITOR

    public event EventHandler? PositionChanged;
    public event EventHandler? OrientationChanged;
    public event EventHandler? ScaleChanged;

    private static readonly int Version = 1;

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        jObject.Add("version", Version);

        var coordinatesObject = new JObject();
        Coordinates.Save(coordinatesObject);
        jObject.Add("coordinates", coordinatesObject);
    }

    public Vector3 Position
    {
        get => Coordinates.LocalPosition;
        set
        {
            Coordinates.LocalPosition = value;
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Vector3 Scale
    {
        get => Coordinates.LocalScale;
        set
        {
            Coordinates.LocalScale = value;
            ScaleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Quaternion Orientation
    {
        get => Coordinates.LocalRotation;
        set
        {
            Coordinates.LocalRotation = value;
            OrientationChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Vector3 Forward => Vector3.Transform(Vector3.Forward, Orientation);
    public Vector3 Up => Vector3.Transform(Vector3.Up, Orientation);

    public BoundingBox BoundingBox => ComputeBoundingBox();

    protected virtual BoundingBox ComputeBoundingBox()
    {
        return new BoundingBox(-Vector3.One / 2f, Vector3.One / 2f);
    }

#endif

    public void Ascend(NodeVisitor nodeVisitor)
    {
        ///call parents
    }

    public void Traverse(NodeVisitor nodeVisitor)
    {

    }
}