using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.Framework.Scripting;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities;

//Actor is the base class for an Object that can be placed or spawned in a level.
public class AActor : UObject
{
    private bool _isEnabled = true;
    private readonly List<ActorComponent> _components = new();
    private readonly List<AActor> _children = new();
    private SceneComponent? _rootComponent;
    public World.World World { get; private set; }

    public bool IsInitialized { get; private set; }
    public AActor? Parent { get; private set; }

    public IEnumerable<AActor> Children => _children;

    public IEnumerable<ActorComponent> Components => _components;

    public SceneComponent? RootComponent
    {
        get => _rootComponent;
        set
        {
#if EDITOR
            if (_rootComponent != null)
            {
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
    public GameplayProxy? GameplayProxy { get; set; }

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

    public AActor()
    {
    }

    public AActor(AActor actor) : base(actor)
    {
        World = actor.World;
        _isEnabled = actor._isEnabled;
        Parent = actor.Parent;
        RootComponent = actor.RootComponent?.Clone() as SceneComponent;
        GameplayProxyClassName = actor.GameplayProxyClassName;
        GameplayProxy = actor.GameplayProxy?.Clone();

        foreach (var component in actor._components)
        {
            AddComponent(component.Clone());
        }

        foreach (var child in actor._children)
        {
            AddChild(child.Clone());
        }
    }

    public AActor Clone()
    {
        return new AActor(this);
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
        RootComponent.OnEnabledValueChange();

        foreach (var child in Children)
        {
            child.OnEnabledValueChange();
        }
    }

    public void AddChild(AActor actor)
    {
        _children.Add(actor);
        actor.Parent = this;

#if EDITOR
        ChildAdded?.Invoke(this, actor);
#endif
    }

    public void RemoveChild(AActor actor)
    {
        _children.Remove(actor);
        actor.Parent = null;

#if EDITOR
        ChildRemoved?.Invoke(this, actor);
#endif
    }

    public void AddComponent(ActorComponent component)
    {
        _components.Add(component);
        component.Attach(this);

#if EDITOR
        ComponentAdded?.Invoke(this, component);
#endif
    }

    public void RemoveComponent(ActorComponent component)
    {
        _components.Remove(component);
        component.Detach();

#if EDITOR
        ComponentRemoved?.Invoke(this, component);
#endif
    }

    public T? GetComponent<T>() where T : class
    {
        if (RootComponent is T)
        {
            return RootComponent as T;
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
        if (!World.Game.IsRunningInGameEditorMode)
        {
            GameplayProxy?.Update(elapsedTime);
        }
#else
        GameplayProxy?.Update(elapsedTime);
#endif
    }

    public void Draw(float elapsedTime)
    {
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

    public override void Load(JsonElement element)
    {
        base.Load(element);

        var node = element.GetProperty("root_component");
        if (node.ValueKind == JsonValueKind.Object)
        {
            RootComponent = ElementFactory.Load<SceneComponent>(node);
        }

        foreach (var componentNode in element.GetProperty("components").EnumerateArray())
        {
            if (componentNode.GetProperty("type").ValueKind == JsonValueKind.Number
                && componentNode.GetProperty("type").GetInt32() == 1
                && componentNode.GetProperty("external_component").ValueKind == JsonValueKind.Object)
            {
                GameplayProxy = ElementFactory.Create<GameplayProxy>(componentNode.GetProperty("external_component").GetProperty("type").GetString());
            }
            else
            {
                AddComponent(ElementFactory.Load<ActorComponent>(componentNode));
            }
        }

        /*foreach (var item in element.GetJsonPropertyByName("childen").Value.EnumerateArray())
        {
            //id of all child
        }*/
    }

#if EDITOR

    public event EventHandler<AActor> ChildAdded;
    public event EventHandler<AActor> ChildRemoved;

    public event EventHandler<ActorComponent> ComponentAdded;
    public event EventHandler<ActorComponent> ComponentRemoved;

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
    }

#endif
}