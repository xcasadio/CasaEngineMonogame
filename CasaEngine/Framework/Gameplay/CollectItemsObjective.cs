namespace CasaEngine.Framework.Gameplay;

public sealed class CollectItemsObjective : GameplayObjective, IGameplayEventListener
{
    public int RequiredCount { get; set; }
    public string RequiredItemId { get; set; } = string.Empty;
    public int CurrentCount { get; private set; }

    public void OnItemCollected()
    {
        CurrentCount++;

        if (CurrentCount >= RequiredCount)
        {
            IsCompleted = true;
        }
    }

    public void OnGameplayEvent(IGameplayEvent gameplayEvent)
    {
        if (gameplayEvent is not ItemCollectedEvent itemCollectedEvent)
        {
            return;
        }

        if (!string.IsNullOrEmpty(RequiredItemId)
            && !string.Equals(RequiredItemId, itemCollectedEvent.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        OnItemCollected();
    }
}