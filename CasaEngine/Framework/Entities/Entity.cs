using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logs;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using BoundingBox = Microsoft.Xna.Framework.BoundingBox;
using Vector3 = Microsoft.Xna.Framework.Vector3;

#if EDITOR
using XNAGizmo;
#endif

namespace CasaEngine.Framework.Entities;

public class Entity : EntityBase
{
    private Entity? _parent;

    public Entity? Parent
    {
        get => _parent;
        set
        {
            _parent = value;
            Coordinates.Parent = _parent?.Coordinates;
        }
    }

    public ComponentManager ComponentManager { get; } = new();

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);
        ComponentManager.Initialize(this);
    }

    protected override void UpdateInternal(float elapsedTime)
    {
        ComponentManager.Update(elapsedTime);
    }

    protected override void DrawInternal()
    {
        ComponentManager.Draw();
    }

    public Entity Clone()
    {
        var entity = new Entity();
        entity.CopyFrom(this);
        return entity;
    }

    public void CopyFrom(Entity entity)
    {
        ComponentManager.CopyFrom(entity.ComponentManager);
        Coordinates.CopyFrom(entity.Coordinates);
        Parent = entity.Parent;

        base.CopyFrom(entity);
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);

        foreach (var item in element.GetJsonPropertyByName("components").Value.EnumerateArray())
        {
            ComponentManager.Components.Add(GameSettings.ComponentLoader.Load(item));
        }
    }

    public override void ScreenResized(int width, int height)
    {
        var components = ComponentManager.Components;

        for (var index = 0; index < components.Count; index++)
        {
            components[index].ScreenResized(width, height);
        }

        base.ScreenResized(width, height);
    }

    protected override void OnEnabledValueChange()
    {
        var components = ComponentManager.Components;

        for (var index = 0; index < components.Count; index++)
        {
            components[index].OnEnabledValueChange();
        }

        base.OnEnabledValueChange();
    }

    public override void OnBeginPlay(World.World world)
    {
        foreach (var component in ComponentManager.Components)
        {
            if (component is GamePlayComponent gamePlayComponent)
            {
                gamePlayComponent.ExternalComponent?.OnBeginPlay(world);
            }
        }

        base.OnBeginPlay(world);
    }

    public override void OnEndPlay(World.World world)
    {
        foreach (var component in ComponentManager.Components)
        {
            if (component is GamePlayComponent gamePlayComponent)
            {
                gamePlayComponent.ExternalComponent?.OnEndPlay(world);
            }
        }

        base.OnEndPlay(world);
    }

#if EDITOR
    private static readonly int Version = 1;

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        //jObject.Add("version", Version);

        var componentsJArray = new JArray();
        foreach (var component in ComponentManager.Components)
        {
            JObject componentObject = new();
            component.Save(componentObject, option);
            componentsJArray.Add(componentObject);
        }
        jObject.Add("components", componentsJArray);
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;
        bool found = false;

        foreach (var component in ComponentManager.Components)
        {
            if (component is IBoundingBoxable boundingBoxComputable)
            {
                var boundingBox = boundingBoxComputable.BoundingBox;
                min = Vector3.Min(min, boundingBox.Min);
                max = Vector3.Max(max, boundingBox.Max);
                found = true;
            }
        }

        return found ? new BoundingBox(min, max) : new BoundingBox(Vector3.One / 2f, Vector3.One / 2f);
    }
#endif
}














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

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            LogManager.Instance.WriteTrace($"Entity {Name} is {(_isEnabled ? "enabled" : "disabled")}");
            OnEnabledValueChange();
        }
    }

    public bool IsVisible { get; set; } = true;

    public bool ToBeRemoved { get; set; }

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
}