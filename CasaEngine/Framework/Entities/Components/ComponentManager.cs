namespace CasaEngine.Framework.Entities.Components;

public class ComponentManager
{
    public Entity Owner { get; }

    public List<Component> Components { get; } = new();

    public ComponentManager(Entity entity)
    {
        Owner = entity;
    }

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

    public void Initialize()
    {
        foreach (var component in Components)
        {
            component.Initialize();
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
}