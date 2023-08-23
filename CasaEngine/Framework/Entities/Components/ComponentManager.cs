using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Entities.Components;

public class ComponentManager
{
    public List<Component> Components { get; } = new();

    public void Update(float elapsedTime)
    {
        foreach (var component in Components)
        {
            component.Update(elapsedTime);
        }
    }

    public void Draw()
    {
        foreach (var component in Components)
        {
            component.Draw();
        }
    }

    public void Initialize(CasaEngineGame game)
    {
        foreach (var component in Components)
        {
            component.Initialize(game);
        }
    }

    public bool HandleMessage(Message msg)
    {
        bool res = false;

        foreach (var component in Components)
        {
            res |= component.HandleMessage(msg);
        }

        return res;
    }

    public void Add(Component component)
    {
        Components.Add(component);
    }

    public void Remove(Component component)
    {
        Components.Remove(component);
    }

    public void Clear()
    {
        Components.Clear();
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

    public void CopyFrom(ComponentManager componentManager, Entity entity)
    {
        foreach (var component in componentManager.Components)
        {
            Components.Add(component.Clone(entity));
        }
    }
}