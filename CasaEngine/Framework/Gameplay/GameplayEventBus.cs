namespace CasaEngine.Framework.Gameplay;

public sealed class GameplayEventBus
{
    private readonly List<IGameplayEventListener> _listeners = [];

    public void Register(IGameplayEventListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        if (!_listeners.Contains(listener))
        {
            _listeners.Add(listener);
        }
    }

    public void Unregister(IGameplayEventListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        _listeners.Remove(listener);
    }

    public void Publish(IGameplayEvent gameplayEvent)
    {
        ArgumentNullException.ThrowIfNull(gameplayEvent);

        for (int index = 0; index < _listeners.Count; index++)
        {
            _listeners[index].OnGameplayEvent(gameplayEvent);
        }
    }
}