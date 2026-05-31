namespace CasaEngine.Framework.Gameplay;

public sealed class CollectItemsObjective : GameplayObjective
{
    public int RequiredCount { get; set; }
    public int CurrentCount { get; private set; }

    public void OnItemCollected()
    {
        CurrentCount++;

        if (CurrentCount >= RequiredCount)
        {
            IsCompleted = true;
        }
    }
}