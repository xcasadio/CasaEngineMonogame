namespace CasaEngine.Framework.AI.Messaging;

public class WorldMessageBus : IWorldMessageBus
{
    private readonly Dictionary<Guid, IMessageable> _endpoints = [];

    public double CurrentSimulationTime { get; private set; }

    public int PendingMessagesCount => 0;

    public virtual void Reset(double currentSimulationTime = 0.0)
    {
        CurrentSimulationTime = currentSimulationTime;
        _endpoints.Clear();
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

    public virtual bool SendMessage(Guid senderId, Guid receiverId, double delayTime, int type, object extraInfo)
    {
        if (delayTime > 0.0)
        {
            return false;
        }

        if (!_endpoints.TryGetValue(receiverId, out IMessageable endpoint))
        {
            return false;
        }

        CurrentSimulationTime = Math.Max(CurrentSimulationTime, 0.0);
        return endpoint.HandleMessage(new Message(senderId, receiverId, type, CurrentSimulationTime, extraInfo));
    }

    public virtual int DispatchDueMessages(double currentSimulationTime)
    {
        CurrentSimulationTime = currentSimulationTime;
        return 0;
    }
}