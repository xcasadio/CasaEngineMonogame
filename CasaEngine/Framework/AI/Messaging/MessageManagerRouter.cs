namespace CasaEngine.Framework.AI.Messaging;

[Obsolete("Use World.MessageBus or IWorldMessageBus instead of the global MessageManagerRouter singleton.")]
public sealed class MessageManagerRouter : IMessageManager
{
    private static readonly MessageManagerRouter Manager = new();
    private readonly WorldMessageBus _legacyBus = new();
    private double _simulationTime;

    static MessageManagerRouter() { }

    private MessageManagerRouter()
    {
    }

    public static MessageManagerRouter Instance => Manager;


    public void ResetManager(double precision)
    {
        _simulationTime = 0.0;
        _legacyBus.Reset();
    }

    public bool RegisterEndpoint(Guid receiverId, IMessageable endpoint)
    {
        return _legacyBus.RegisterEndpoint(receiverId, endpoint);
    }

    public bool UnregisterEndpoint(Guid receiverId)
    {
        return _legacyBus.UnregisterEndpoint(receiverId);
    }

    public int AdvanceSimulationTime(double elapsedTime)
    {
        _simulationTime += Math.Max(0.0, elapsedTime);
        return _legacyBus.DispatchDueMessages(_simulationTime);
    }

    public void SendMessage(Guid senderId, Guid receiverId, double delayTime, int type, object extraInfo)
    {
        _legacyBus.SendMessage(senderId, receiverId, delayTime, type, extraInfo);
    }

    public void Update()
    {
        _legacyBus.DispatchDueMessages(_simulationTime);
    }
}