using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement.Components;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement;

//Actor is the base class for an Object that can be placed or spawned in a level.
public class AActor : UObject
{
    private bool _isEnabled = true;
    private readonly List<ActorComponent> _components = new();
    private readonly List<AActor> _children = new();
    private SceneComponent? _rootComponent;

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
            //LogManager.Instance.WriteTrace($"Entity {Name} is {(_isEnabled ? "enabled" : "disabled")}");
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
        _isEnabled = actor._isEnabled;
        Parent = actor.Parent;
        RootComponent = actor.RootComponent;
        GameplayProxyClassName = actor.GameplayProxyClassName;
        GameplayProxy = actor.GameplayProxy;

        foreach (var component in actor._components)
        {
            _components.Add(component.Clone());
        }

        foreach (var child in actor._children)
        {
            _children.Add(child.Clone());
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

        GameplayProxy?.Update(elapsedTime);
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
            AddComponent(ElementFactory.Load<ActorComponent>(componentNode));
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