using CasaEngine.Framework.Scene.Entities;

namespace CasaEngine.Framework.Gameplay;

public readonly struct ItemCollectedEvent : IGameplayEvent
{
    public ItemCollectedEvent(Entity entity, string itemId)
    {
        Entity = entity;
        ItemId = itemId ?? string.Empty;
    }

    public Entity Entity { get; }

    public string ItemId { get; }
}