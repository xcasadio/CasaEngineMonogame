using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using CasaEngine.Core.Log;

namespace CasaEngine.Framework.Entities;

public sealed class EntityNameChangedEventArgs : EventArgs
{
    public EntityNameChangedEventArgs(string previousName, string newName)
    {
        PreviousName = previousName;
        NewName = newName;
    }

    public string PreviousName { get; }

    public string NewName { get; }
}

//Entity is the base class for an Object that can be placed or spawned in a level.
public class Entity : ObjectBase
{
    private bool _isEnabled = true;
    private readonly List<EntityComponent> _components = [];
    private readonly List<Entity> _children = [];
    private SceneComponent? _rootComponent;
    public World.World World { get; private set; }

    public bool IsInitialized { get; private set; }
    public Entity? Parent { get; private set; }

    public IEnumerable<Entity> Children => _children;

    public IEnumerable<EntityComponent> Components => _components;

    public new string Name
    {
        get => base.Name;
        set
        {
            if (base.Name == value)
            {
                return;
            }

            var previousName = base.Name;
            base.Name = value;
            NameChanged?.Invoke(this, new EntityNameChangedEventArgs(previousName, value));
        }
    }

    public SceneComponent? RootComponent
    {
        get => _rootComponent;
        set
        {
            if (_rootComponent != null)
            {
                _rootComponent.Detach();
                ComponentRemoved?.Invoke(this, _rootComponent);
            }

            _rootComponent = value;

            if (_rootComponent != null)
            {
                _rootComponent.Attach(this);
                ComponentAdded?.Invoke(this, _rootComponent);
            }
        }
    }

    public string GameplayProxyClassName { get; set; }
    public IGameplayProxy? GameplayProxy { get; private set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            //Logs.WriteTrace($"Entity {Name} is {(_isEnabled ? "enabled" : "disabled")}");
            OnEnabledValueChange();
        }
    }

    public bool IsVisible { get; set; } = true;

    public bool ToBeRemoved { get; private set; }

    public Entity()
    {
    }

    public Entity(Entity entity) : base(entity)
    {
        World = entity.World;
        _isEnabled = entity._isEnabled;
        Parent = entity.Parent;
        RootComponent = entity.RootComponent?.Clone() as SceneComponent;
        GameplayProxyClassName = entity.GameplayProxyClassName;
        GameplayProxy = entity.GameplayProxy?.Clone();

        foreach (var component in entity._components)
        {
            AddComponent(component.Clone());
        }

        foreach (var child in entity._children)
        {
            AddChild(child.Clone());
        }
    }

    public virtual Entity Clone()
    {
        return new Entity(this);
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        RootComponent?.Initialize();

        for (int i = 0; i < _components.Count; i++)
        {
            _components[i].Initialize();
        }

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Initialize();
        }

        if (!string.IsNullOrWhiteSpace(GameplayProxyClassName))
        {
            GameplayProxy = ElementFactory.Create<GameplayProxy>(GameplayProxyClassName);
        }

        GameplayProxy?.Initialize(this);

        IsInitialized = true;
    }

    public void InitializeWithWorld(World.World world)
    {
        World = world;

        RootComponent?.InitializeWithWorld(world);

        for (int i = 0; i < _components.Count; i++)
        {
            _components[i].InitializeWithWorld(world);
        }

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].InitializeWithWorld(world);
        }

        GameplayProxy?.InitializeWithWorld(world);
    }

    private void OnEnabledValueChange()
    {
        RootComponent?.OnEnabledValueChange();

        for (int i = 0; i < _components.Count; i++)
        {
            if (_components[i] is SceneComponent sceneComponent)
            {
                sceneComponent.OnEnabledValueChange();
            }
        }

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].OnEnabledValueChange();
        }
    }

    public void AddChild(Entity actor)
    {
        _children.Add(actor);
        actor.Parent = this;
        ChildAdded?.Invoke(this, actor);
    }

    public void RemoveChild(Entity actor)
    {
        _children.Remove(actor);
        actor.Parent = null;
        ChildRemoved?.Invoke(this, actor);
    }

    public void AddComponent(EntityComponent component)
    {
        _components.Add(component);
        component.Attach(this);
        ComponentAdded?.Invoke(this, component);
    }

    public void RemoveComponent(EntityComponent component)
    {
        _components.Remove(component);
        component.Detach();
        ComponentRemoved?.Invoke(this, component);
    }

    public T GetRequiredComponent<T>() where T : class
    {
        T component = GetComponent<T>();
        if (component == null)
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} is missing on {Name}.");
        }

        return component;
    }

    public T? GetComponent<T>() where T : class
    {
        if (RootComponent != null)
        {
            if (RootComponent is T component)
            {
                return component;
            }

            for (int i = 0; i < RootComponent.Children.Count; i++)
            {
                if (RootComponent.Children[i] is T child)
                {
                    return child;
                }
            }
        }

        for (int i = 0; i < _components.Count; i++)
        {
            if (_components[i] is T component)
            {
                return component;
            }
        }

        return null;
    }

    public BoundingBox GetBoundingBox()
    {
        var boundingBox = RootComponent?.BoundingBox ?? new BoundingBox();

        foreach (var component in Components)
        {
            if (component is SceneComponent sceneComponent)
            {
                boundingBox.ExpandBy(sceneComponent.BoundingBox);
            }
        }

        return boundingBox;
    }

    public void ReActivate()
    {
        ToBeRemoved = false;
        IsEnabled = true;
        IsVisible = true;
        Logs.WriteTrace($"Entity reactivated : {Name} {Id}");
    }

    public void Destroy()
    {
        ToBeRemoved = true;
        IsEnabled = false;
        IsVisible = false;
        Logs.WriteTrace($"Entity destroyed : {Name} {Id}");
    }

    public void Update(float elapsedTime)
    {
        RootComponent?.Update(elapsedTime);

        for (int i = 0; i < _components.Count; i++)
        {
            _components[i].Update(elapsedTime);
        }

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Update(elapsedTime);
        }

    if (World?.Game?.ExecutionPolicy.UpdateGameplayScripts ?? false)
        {
            GameplayProxy?.Update(elapsedTime);
        }
    }

    public void Draw(float elapsedTime)
    {
        if (!IsVisible)
        {
            return;
        }

        RootComponent?.Draw(elapsedTime);

        for (int i = 0; i < _components.Count; i++)
        {
            if (_components[i] is PrimitiveComponent sceneComponent)
            {
                sceneComponent.Draw(elapsedTime);
            }
        }

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Draw(elapsedTime);
        }

        GameplayProxy?.Draw();
    }

    public void OnScreenResized(int width, int height)
    {
        RootComponent?.OnScreenResized(width, height);

        for (int i = 0; i < _components.Count; i++)
        {
            if (_components[i] is SceneComponent sceneComponent)
            {
                sceneComponent.OnScreenResized(width, height);
            }
        }
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        GameplayProxyClassName = element["script_class_name"].GetString();

        var node = element["root_component"];
        if (node.Type == JTokenType.Object)
        {
            RootComponent = ElementFactory.Load<SceneComponent>((JObject)node);
        }

        foreach (var componentNode in element["components"])
        {
            var entityComponent = ElementFactory.Load<EntityComponent>((JObject)componentNode);
            //_components.Add(entityComponent);
            AddComponent(entityComponent);
        }
    }

    public event EventHandler<Entity> ChildAdded;
    public event EventHandler<Entity> ChildRemoved;

    public event EventHandler<EntityComponent> ComponentAdded;
    public event EventHandler<EntityComponent> ComponentRemoved;

    public event EventHandler<EntityNameChangedEventArgs> NameChanged;

    public override void Save(JObject node)
    {
        throw new NotSupportedException("Entity authoring serialization lives in CasaEngine.EditorServices.");
    }
}