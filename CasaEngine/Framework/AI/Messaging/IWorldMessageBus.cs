namespace CasaEngine.Framework.AI.Messaging;

public interface IWorldMessageBus
{
    double CurrentSimulationTime { get; }

    int PendingMessagesCount { get; }

    void Reset(double currentSimulationTime = 0.0);

    bool RegisterEndpoint(Guid receiverId, IMessageable endpoint);

    bool UnregisterEndpoint(Guid receiverId);

    bool SendMessage(Guid senderId, Guid receiverId, double delayTime, int type, object extraInfo);

    int DispatchDueMessages(double currentSimulationTime);
}