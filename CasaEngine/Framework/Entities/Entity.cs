using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Objects;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

#if EDITOR
using FlowGraph;
#endif

namespace CasaEngine.Framework.Entities;

//Entity is the base class for an Object that can be placed or spawned in a level.
public class Entity : ObjectBase
{
    private bool _isEnabled = true;
    private readonly List<EntityComponent> _components = new();
    private readonly List<Entity> _children = new();
    private SceneComponent? _rootComponent;
    public World.World World { get; private set; }

    public bool IsInitialized { get; private set; }
    public Entity? Parent { get; private set; }

    public IEnumerable<Entity> Children => _children;

    public IEnumerable<EntityComponent> Components => _components;

    public SceneComponent? RootComponent
    {
        get => _rootComponent;
        set
        {
#if EDITOR
            if (_rootComponent != null)
            {
                _rootComponent.Detach();
                ComponentRemoved?.Invoke(this, _rootComponent);
            }
#endif

            _rootComponent = value;

            if (_rootComponent != null)
            {
                _rootComponent.Attach(this);
#if EDITOR
                ComponentAdded?.Invoke(this, _rootComponent);
#endif
            }
        }
    }

    public string GameplayProxyClassName { get; set; }
    public GameplayProxy? GameplayProxy { get; private set; }

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
#if EDITOR
        FlowGraph = new();
#endif
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

#if EDITOR
        FlowGraph = entity.FlowGraph; //.Clone(); bug!!!
#endif
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

#if !EDITOR
        GameplayProxy?.Initialize(this);
#endif

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

#if !EDITOR
        GameplayProxy?.InitializeWithWorld(world);
#endif
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

#if EDITOR
        ChildAdded?.Invoke(this, actor);
#endif
    }

    public void RemoveChild(Entity actor)
    {
        _children.Remove(actor);
        actor.Parent = null;

#if EDITOR
        ChildRemoved?.Invoke(this, actor);
#endif
    }

    public void AddComponent(EntityComponent component)
    {
        _components.Add(component);
        component.Attach(this);

#if EDITOR
        ComponentAdded?.Invoke(this, component);
#endif
    }

    public void RemoveComponent(EntityComponent component)
    {
        _components.Remove(component);
        component.Detach();

#if EDITOR
        ComponentRemoved?.Invoke(this, component);
#endif
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
    }

    public void Destroy()
    {
        ToBeRemoved = true;
        IsEnabled = false;
        IsVisible = false;
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

#if EDITOR
        if (!World?.Game?.IsRunningInGameEditorMode ?? false)
        {
            GameplayProxy?.Update(elapsedTime);
        }
#else
        GameplayProxy?.Update(elapsedTime);
#endif
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
            _components.Add(ElementFactory.Load<EntityComponent>((JObject)componentNode));
        }

#if EDITOR
        if (element.TryGetValue("flow_graph", out var flowGraphNode))
        {
            FlowGraph.Load((JObject)flowGraphNode);
        }
#endif
    }

#if EDITOR

    public event EventHandler<Entity> ChildAdded;
    public event EventHandler<Entity> ChildRemoved;

    public event EventHandler<EntityComponent> ComponentAdded;
    public event EventHandler<EntityComponent> ComponentRemoved;

    public FlowGraphManager FlowGraph { get; private set; }

    public override void Save(JObject node)
    {
        base.Save(node);

        if (RootComponent != null)
        {
            JObject rootComponentNode = new();
            RootComponent.Save(rootComponentNode);
            node.Add("root_component", rootComponentNode);
        }
        else
        {
            node.Add("root_component", "null");
        }

        var componentsJArray = new JArray();
        foreach (var component in _components)
        {
            JObject componentObject = new();
            component.Save(componentObject);
            componentsJArray.Add(componentObject);
        }
        node.Add("components", componentsJArray);

        node.Add("script_class_name", GameplayProxyClassName);

        //TODO only in editor
        var flowGraphNode = new JObject();
        FlowGraph.Save(flowGraphNode);
        node.Add("flow_graph", flowGraphNode);
    }

#endif
}