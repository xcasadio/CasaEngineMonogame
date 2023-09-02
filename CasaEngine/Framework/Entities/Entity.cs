using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logger;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using XNAGizmo;
using BoundingBox = Microsoft.Xna.Framework.BoundingBox;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Entities;

public class Entity : ISaveLoad
#if EDITOR
    , ITransformable
#endif
{
    private Entity? _parent;
    private bool _isEnabled = true;

    [Category("Object"), ReadOnly(true)]
    public long Id { get; private set; }

    [Category("Object")]
    public string Name { get; set; } = string.Empty;

    [Browsable(false)]
    public Entity? Parent
    {
        get => _parent;
        set
        {
            _parent = value;
            Coordinates.Parent = _parent?.Coordinates;
        }
    }

    [Browsable(false)] public ComponentManager ComponentManager { get; } = new();

    [Category("Object")]
    public Coordinates Coordinates { get; } = new();

    [Category("Object")]
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            LogManager.Instance.WriteLineTrace($"Entity {Name} is {(_isEnabled ? "enabled" : "disabled")}");
            OnEnabledValueChange();
        }
    }

    [Category("Object")]
    public bool IsVisible { get; set; } = true;

    [Browsable(false)]
    public bool ToBeRemoved { get; set; }

    [Category("Object"), ReadOnly(true)]
    public bool IsTemporary { get; internal set; }

    public event EventHandler<EventCollisionArgs> OnHit;
    public event EventHandler<EventCollisionArgs> OnHitEnded;

    public Entity()
    {
        Id = IdManager.GetId();
    }

    public void Initialize(CasaEngineGame game)
    {
        ComponentManager.Initialize(game);
    }

    public void Update(float elapsedTime)
    {
        if (IsEnabled == false)
        {
            return;
        }

        ComponentManager.Update(elapsedTime);
    }

    public void Draw()
    {
        if (IsVisible == false)
        {
            return;
        }

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
        ComponentManager.CopyFrom(entity.ComponentManager, this);
        Coordinates.CopyFrom(entity.Coordinates);

        IsTemporary = entity.IsTemporary;
        Name = entity.Name + $"_{Id}";
        Parent = entity.Parent;
        IsEnabled = entity.IsEnabled;
        IsVisible = entity.IsVisible;
    }

    public void Destroy()
    {
        ToBeRemoved = true;
    }

    public void Load(JsonElement element, SaveOption option)
    {
        var version = element.GetJsonPropertyByName("version").Value.GetInt32();
        Name = element.GetJsonPropertyByName("name").Value.GetString();
        Id = element.GetJsonPropertyByName("id").Value.GetInt32();

        foreach (var item in element.GetJsonPropertyByName("components").Value.EnumerateArray())
        {
            ComponentManager.Components.Add(GameSettings.ComponentLoader.Load(this, item));
        }

        var jsonCoordinate = element.GetJsonPropertyByName("coordinates").Value;
        Coordinates.Load(jsonCoordinate);
    }

    public void ScreenResized(int width, int height)
    {
        var components = ComponentManager.Components;

        for (var index = 0; index < components.Count; index++)
        {
            components[index].ScreenResized(width, height);
        }
    }

    private void OnEnabledValueChange()
    {
        var components = ComponentManager.Components;

        for (var index = 0; index < components.Count; index++)
        {
            components[index].OnEnabledValueChange();
        }
    }

    public void Hit(Collision collision, Components.Component component)
    {
        //LogManager.Instance.WriteLineTrace($"OnHit : {collision.ColliderA.Owner.Name} & {collision.ColliderB.Owner.Name}");
        OnHit?.Invoke(this, new EventCollisionArgs(collision, component));
    }

    public void HitEnded(Collision collision, Components.Component component)
    {
        //LogManager.Instance.WriteLineTrace($"OnHitEnded : {collision.ColliderA.Owner.Name} & {collision.ColliderB.Owner.Name}");
        OnHitEnded?.Invoke(this, new EventCollisionArgs(collision, component));
    }

#if EDITOR
    public event EventHandler? PositionChanged;
    public event EventHandler? OrientationChanged;
    public event EventHandler? ScaleChanged;

    private static readonly int Version = 1;

    public void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("version", 1);
        jObject.Add("id", Id);
        jObject.Add("name", Name);

        var coordinatesObject = new JObject();
        Coordinates.Save(coordinatesObject);
        jObject.Add("coordinates", coordinatesObject);

        var componentsJArray = new JArray();
        foreach (var component in ComponentManager.Components)
        {
            JObject componentObject = new();
            component.Save(componentObject, option);
            componentsJArray.Add(componentObject);
        }
        jObject.Add("components", componentsJArray);
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

    //TODO : compute real BoundingBox

    public BoundingBox BoundingBox => ComputeBoundingBox();

    private BoundingBox ComputeBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;
        bool found = false;

        foreach (var component in ComponentManager.Components)
        {
            if (component is IBoundingBoxComputable boundingBoxComputable)
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