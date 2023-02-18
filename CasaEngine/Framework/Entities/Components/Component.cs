using System.Text.Json;

namespace CasaEngine.Framework.Entities.Components;

public abstract class Component
{
    public Entity Owner { get; }
    public int Type { get; }

    protected Component(Entity entity, int type)
    {
        Owner = entity;
        Type = type;
    }

    public virtual void Initialize()
    {

    }

    public abstract void Update(float elapsedTime);

    public virtual void Draw()
    {
    }

    public bool HandleMessage(Message message)
    {
        return false;
    }

    public abstract void Load(JsonElement element);
}