namespace CasaEngine.Framework.Entities.Components;

public class ComponentManager
{
    private readonly List<Component> _components = new();

    public IReadOnlyCollection<Component> Components => _components;

    public void Update(float elapsedTime)
    {
        foreach (var component in _components)
        {
            component.Update(elapsedTime);
        }
    }

    public void Draw()
    {
        foreach (var component in _components)
        {
            component.Draw();
        }
    }

    public void Initialize(Entity entity)
    {
        foreach (var component in _components)
        {
            component.Initialize(entity);
        }
    }

    public void Add(Component component)
    {
        _components.Add(component);

#if EDITOR
        ComponentAdded?.Invoke(this, component);
#endif
    }

    public void Remove(Component component)
    {
        _components.Remove(component);

#if EDITOR
        ComponentRemoved?.Invoke(this, component);
#endif
    }

    public void Clear()
    {
        _components.Clear();

#if EDITOR
        ComponentClear?.Invoke(this, EventArgs.Empty);
#endif
    }

    public List<T> GetComponents<T>() where T : Component
    {
        var components = new List<T>();

        foreach (var component in Components)
        {
            if (component is T component1)
            {
                components.Add(component1);
            }
        }

        return components;
    }

    public T? GetComponent<T>() where T : Component
    {
        foreach (var component in Components)
        {
            if (component is T component1)
            {
                return component1;
            }
        }

        return null;
    }

    public void CopyFrom(ComponentManager componentManager)
    {
        foreach (var component in componentManager.Components)
        {
            _components.Add(component.Clone());
        }
    }

    public void ScreenResized(int width, int height)
    {
        for (var index = 0; index < _components.Count; index++)
        {
            _components[index].ScreenResized(width, height);
        }
    }

    public void OnEnabledValueChange()
    {
        for (var index = 0; index < _components.Count; index++)
        {
            _components[index].OnEnabledValueChange();
        }
    }

#if EDITOR

    public event EventHandler<Component> ComponentAdded;
    public event EventHandler<Component> ComponentRemoved;
    public event EventHandler? ComponentClear;


#endif
}