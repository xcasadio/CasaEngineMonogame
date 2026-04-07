using CasaEngine.Framework.Scene.Entities;

namespace CasaEngine.Framework.AI.Messaging;

public class WorldMessageBus : IWorldMessageBus
{
    private sealed record ScheduledMessage(Message Message, long Sequence);

    private readonly Dictionary<Guid, IMessageable> _endpoints = [];
    private readonly List<ScheduledMessage> _scheduledMessages = [];
    private long _nextSequence;

    public double CurrentSimulationTime { get; private set; }

    public int PendingMessagesCount => _scheduledMessages.Count;

    public virtual void Reset(double currentSimulationTime = 0.0)
    {
        CurrentSimulationTime = currentSimulationTime;
        _endpoints.Clear();
        _scheduledMessages.Clear();
        _nextSequence = 0;
    }

    public virtual bool RegisterEndpoint(Guid receiverId, IMessageable endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (receiverId == Guid.Empty)
        {
            throw new ArgumentException("Receiver id must not be empty.", nameof(receiverId));
        }

        _endpoints[receiverId] = endpoint;
        return true;
    }

    public virtual bool UnregisterEndpoint(Guid receiverId)
    {
        return _endpoints.Remove(receiverId);
    }

    public virtual bool RegisterEntity(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        bool registered = false;

        if (TryResolveEndpoint(entity, out IMessageable endpoint))
        {
            RegisterEndpoint(entity.Id, endpoint);
            registered = true;
        }

        foreach (Entity child in entity.Children)
        {
            registered |= RegisterEntity(child);
        }

        return registered;
    }

    public virtual bool UnregisterEntity(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        bool removed = UnregisterEndpoint(entity.Id);

        foreach (Entity child in entity.Children)
        {
            removed |= UnregisterEntity(child);
        }

        return removed;
    }

    public virtual bool SendMessage(Guid senderId, Guid receiverId, double delayTime, int type, object extraInfo)
    {
        Message message = new(senderId, receiverId, type, CurrentSimulationTime, extraInfo);

        if (delayTime <= 0.0)
        {
            return TryDispatch(message);
        }

        message.DispatchTime = CurrentSimulationTime + delayTime;
        Enqueue(message);
        return true;
    }

    public virtual int DispatchDueMessages(double currentSimulationTime)
    {
        CurrentSimulationTime = currentSimulationTime;
        int dispatchCount = 0;

        while (_scheduledMessages.Count > 0 && _scheduledMessages[0].Message.DispatchTime <= currentSimulationTime)
        {
            ScheduledMessage scheduledMessage = _scheduledMessages[0];
            _scheduledMessages.RemoveAt(0);
            if (TryDispatch(scheduledMessage.Message))
            {
                dispatchCount += 1;
            }
        }

        return dispatchCount;
    }

    protected virtual bool TryDispatch(Message message)
    {
        if (!_endpoints.TryGetValue(message.RecieverID, out IMessageable endpoint))
        {
            return false;
        }

        return endpoint.HandleMessage(message);
    }

    private void Enqueue(Message message)
    {
        ScheduledMessage scheduledMessage = new(message, _nextSequence++);
        int insertIndex = _scheduledMessages.BinarySearch(scheduledMessage, ScheduledMessageComparer.Instance);
        if (insertIndex < 0)
        {
            insertIndex = ~insertIndex;
        }

        _scheduledMessages.Insert(insertIndex, scheduledMessage);
    }

    protected virtual bool TryResolveEndpoint(Entity entity, out IMessageable endpoint)
    {
        endpoint = entity as IMessageable
            ?? entity.GetComponent<IMessageable>()
            ?? entity.GameplayProxy as IMessageable;
        return endpoint != null;
    }

    private sealed class ScheduledMessageComparer : IComparer<ScheduledMessage>
    {
        public static ScheduledMessageComparer Instance { get; } = new();

        public int Compare(ScheduledMessage x, ScheduledMessage y)
        {
            int timeComparison = x.Message.DispatchTime.CompareTo(y.Message.DispatchTime);
            if (timeComparison != 0)
            {
                return timeComparison;
            }

            return x.Sequence.CompareTo(y.Sequence);
        }
    }
}