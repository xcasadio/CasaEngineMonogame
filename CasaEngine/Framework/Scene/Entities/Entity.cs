using System.Collections;
using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using CasaEngine.Core.Logging;

namespace CasaEngine.Framework.Scene.Entities;

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
    private readonly List<EntityComponent> _componentsForUpdate = [];
    private readonly List<Entity> _children = [];
    private SceneComponent _rootComponent;
    public CasaEngine.Framework.Scene.World.World World { get; private set; }

    public bool IsInitialized { get; private set; }
    public Entity Parent { get; private set; }

    public IEnumerable<Entity> Children => _children;

    public IEnumerable<EntityComponent> Components => _components;

    internal IReadOnlyList<Entity> ChildList => _children;

    internal IReadOnlyList<EntityComponent> ComponentList => _components;

    public EntityPolicyState Policies { get; private set; } = new();

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

    public SceneComponent RootComponent
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
            Policies.InvalidateConfiguredPolicySet();

            if (_rootComponent != null)
            {
                _rootComponent.Attach(this);
                ComponentAdded?.Invoke(this, _rootComponent);
            }
        }
    }

    public string GameplayProxyClassName { get; set; }
    public IGameplayProxy GameplayProxy { get; private set; }

    //state loaded for the proxy, applied when the proxy is created; kept afterwards so
    //an entity saved before being initialized does not lose it
    internal JObject GameplayProxyState => _gameplayProxyState;
    private JObject _gameplayProxyState;

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

    [EditorBrowsable(EditorBrowsableState.Never)]
    public EntityPolicySourceMode PolicySourceMode
    {
        get => Policies.PolicySourceMode;
        set => Policies.PolicySourceMode = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public Mobility Mobility
    {
        get => Policies.Mobility;
        set => Policies.Mobility = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public TickPolicy TickPolicy
    {
        get => Policies.TickPolicy;
        set => Policies.TickPolicy = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public SpatialPolicy SpatialPolicy
    {
        get => Policies.SpatialPolicy;
        set => Policies.SpatialPolicy = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public RenderDynamicPolicy RenderDynamicPolicy
    {
        get => Policies.RenderDynamicPolicy;
        set => Policies.RenderDynamicPolicy = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("Use PolicySourceMode and TickPolicy instead of UpdatesEnabled.")]
    public bool UpdatesEnabled
    {
        get => EntityPolicyResolver.ResolveRuntimePolicies(this).ShouldUpdateThisFrame;
        set => Policies.SetLegacyUpdatesEnabledOverride(value);
    }

    public bool ToBeRemoved { get; private set; }

    public Entity()
    {
    }

    /// <summary>Additive constructor for callers assigning a deterministic id (see <see cref="ObjectBase(Guid)"/>).</summary>
    public Entity(Guid id) : base(id)
    {
    }

    public Entity(Entity entity) : base(entity)
    {
        World = entity.World;
        _isEnabled = entity._isEnabled;
        IsVisible = entity.IsVisible;
        Policies = entity.Policies.Clone();
        Parent = entity.Parent;
        RootComponent = entity.RootComponent?.Clone() as SceneComponent;
        GameplayProxyClassName = entity.GameplayProxyClassName;
        GameplayProxy = entity.GameplayProxy?.Clone();
        _gameplayProxyState = entity._gameplayProxyState;

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

        //a proxy cloned or already carrying state must survive; recreate only when missing
        //or when the class name no longer matches (editor changed the script class)
        if (!string.IsNullOrWhiteSpace(GameplayProxyClassName)
            && (GameplayProxy == null
                || !string.Equals(GameplayProxy.GetType().Name, GameplayProxyClassName, StringComparison.InvariantCultureIgnoreCase)))
        {
            GameplayProxy = ElementFactory.Create<GameplayProxy>(GameplayProxyClassName);

            if (GameplayProxy != null && _gameplayProxyState != null)
            {
                GameplayProxy.Load(_gameplayProxyState);
            }
        }

        GameplayProxy?.Initialize(this);

        IsInitialized = true;
    }

    public void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
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
        ArgumentNullException.ThrowIfNull(component);

        _components.Add(component);
        _componentsForUpdate.Add(component);
        component.Attach(this);

        if (IsInitialized)
        {
            component.Initialize();

            if (World != null)
            {
                component.InitializeWithWorld(World);
            }
        }

        Policies.InvalidateConfiguredPolicySet();
        ComponentAdded?.Invoke(this, component);
    }

    public void RemoveComponent(EntityComponent component)
    {
        _components.Remove(component);
        _componentsForUpdate.Remove(component);
        component.Detach();
        Policies.InvalidateConfiguredPolicySet();
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

    public T GetComponent<T>() where T : class
    {
        static T FindSceneComponentRecursive(SceneComponent sceneComponent)
        {
            if (sceneComponent == null)
            {
                return null;
            }

            if (sceneComponent is T component)
            {
                return component;
            }

            for (int index = 0; index < sceneComponent.Children.Count; index++)
            {
                T childComponent = FindSceneComponentRecursive(sceneComponent.Children[index]);
                if (childComponent != null)
                {
                    return childComponent;
                }
            }

            return null;
        }

        T rootComponent = FindSceneComponentRecursive(RootComponent);
        if (rootComponent != null)
        {
            return rootComponent;
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

    public void ClearBoundingBoxDirty()
    {
        RootComponent?.ClearBoundingBoxDirtyRecursive();

        foreach (var component in Components)
        {
            if (component is SceneComponent sceneComponent)
            {
                sceneComponent.ClearBoundingBoxDirtyRecursive();
            }
        }
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
        StopAllOwnedCoroutines();
        ToBeRemoved = true;
        IsEnabled = false;
        IsVisible = false;
        Logs.WriteTrace($"Entity destroyed : {Name} {Id}");
    }

    public CoroutineHandle StartCoroutine(IEnumerator routine)
    {
        return GetCoroutineManager().StartCoroutine(routine, this);
    }

    public CoroutineHandle StartCoroutine(IEnumerator routine, string name)
    {
        return GetCoroutineManager().StartCoroutine(routine, this, name);
    }

    public void StopCoroutine(CoroutineHandle handle)
    {
        World?.RuntimeSystems.CoroutineManager.StopCoroutine(handle);
    }

    public void StopAllCoroutines()
    {
        World?.RuntimeSystems.CoroutineManager.StopAllCoroutines(this);
    }

    internal void StopAllOwnedCoroutines()
    {
        var coroutineManager = World?.RuntimeSystems.CoroutineManager;
        if (coroutineManager == null)
        {
            return;
        }

        coroutineManager.StopAllCoroutines(this);

        if (_rootComponent != null)
        {
            StopComponentCoroutines(_rootComponent, coroutineManager);
        }

        for (int i = 0; i < _components.Count; i++)
        {
            StopComponentCoroutines(_components[i], coroutineManager);
        }

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].StopAllOwnedCoroutines();
        }
    }

    private CoroutineManager GetCoroutineManager()
    {
        var coroutineManager = World?.RuntimeSystems.CoroutineManager;
        if (coroutineManager == null)
        {
            throw new InvalidOperationException($"Entity '{Name}' is not attached to a World.");
        }

        return coroutineManager;
    }

    private static void StopComponentCoroutines(EntityComponent component, CoroutineManager coroutineManager)
    {
        coroutineManager.StopAllCoroutines(component);

        if (component is SceneComponent sceneComponent)
        {
            for (int i = 0; i < sceneComponent.Children.Count; i++)
            {
                StopComponentCoroutines(sceneComponent.Children[i], coroutineManager);
            }
        }
    }

    public void Update(float elapsedTime)
    {
        RootComponent?.Update(elapsedTime);

        for (int i = 0; i < _componentsForUpdate.Count; i++)
        {
            EntityComponent component = _componentsForUpdate[i];
            if (World != null && component is IWorldSystemDrivenComponent)
            {
                continue;
            }

            component.Update(elapsedTime);
        }

        for (int i = 0; i < _children.Count; i++)
        {
            ResolvedEntityPolicies childPolicies = EntityPolicyResolver.ResolveRuntimePolicies(_children[i]);
            if (!childPolicies.ShouldUpdateThisFrame)
            {
                continue;
            }

            _children[i].Update(elapsedTime);
            _children[i].Policies.ClearConditionalUpdateRequest();
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

        if (element.ContainsKey("policy_source"))
        {
            Policies.PolicySourceMode = element["policy_source"]!.GetEnum<EntityPolicySourceMode>();
        }

        if (element.ContainsKey("mobility"))
        {
            Policies.Mobility = element["mobility"]!.GetEnum<Mobility>();
        }

        if (element.ContainsKey("tick_policy"))
        {
            Policies.TickPolicy = element["tick_policy"]!.GetEnum<TickPolicy>();
        }

        if (element.ContainsKey("spatial_policy"))
        {
            Policies.SpatialPolicy = element["spatial_policy"]!.GetEnum<SpatialPolicy>();
        }

        if (element.ContainsKey("render_dynamic_policy"))
        {
            Policies.RenderDynamicPolicy = element["render_dynamic_policy"]!.GetEnum<RenderDynamicPolicy>();
        }

        if (element.ContainsKey("updates_enabled"))
        {
            UpdatesEnabled = element["updates_enabled"]!.GetBoolean();
        }

        GameplayProxyClassName = element["script_class_name"].GetString();
        _gameplayProxyState = element["script"] as JObject;

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

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ApplyExplicitPolicies(EntityPolicySet policySet)
    {
        Policies.ApplyExplicitPolicies(policySet);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void UseEnginePolicyDefaults()
    {
        Policies.UseEnginePolicyDefaults();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RequestConditionalUpdate()
    {
        Policies.RequestConditionalUpdate();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public EntityPolicySet GetEffectivePolicySet()
    {
        return Policies.GetConfiguredPolicySet(this);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResolvedEntityPolicies GetResolvedPolicies()
    {
        return EntityPolicyResolver.ResolveRuntimePolicies(this);
    }

    internal IReadOnlyList<EntityComponent> AttachedComponents => _components;

    public event EventHandler<Entity> ChildAdded;
    public event EventHandler<Entity> ChildRemoved;

    public event EventHandler<EntityComponent> ComponentAdded;
    public event EventHandler<EntityComponent> ComponentRemoved;

    public event EventHandler<EntityNameChangedEventArgs> NameChanged;
}