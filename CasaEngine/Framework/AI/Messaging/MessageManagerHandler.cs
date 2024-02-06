using CasaEngine.Core.Collections;

namespace CasaEngine.Framework.AI.Messaging;

public sealed class MessageManagerHandler : IMessageManager
{

    private static readonly MessageManagerHandler Manager = new();

    internal UniquePriorityQueue<Message> MessageQueue;

    internal Dictionary<int, Dictionary<Guid, MessageHandlerDelegate>> RegisteredEntities;



    static MessageManagerHandler() { }

    private MessageManagerHandler()
    {
        MessageQueue = new UniquePriorityQueue<Message>(new MessageComparer(1000));
        RegisteredEntities = new Dictionary<int, Dictionary<Guid, MessageHandlerDelegate>>();
    }



    public static MessageManagerHandler Instance => Manager;


    public void ResetManager(double precision)
    {
        MessageQueue = new UniquePriorityQueue<Message>(new MessageComparer(precision));
        RegisteredEntities = new Dictionary<int, Dictionary<Guid, MessageHandlerDelegate>>();
    }

    public void RegisterForMessage(int type, Guid entityId, MessageHandlerDelegate handler)
    {
        //If the table for this type of message didn´t exist we set it up and register ourselves
        if (RegisteredEntities[type] == null)
        {
            RegisteredEntities[type] = new Dictionary<Guid, MessageHandlerDelegate>();
            RegisteredEntities[type][entityId] = handler;
            return;
        }

        //If another handler existed it will be overriden
        RegisteredEntities[type][entityId] = handler;
    }

    public void UnregisterForMessage(int type, Guid entityId)
    {
        if (RegisteredEntities[type] == null)
        {
            return;
        }

        RegisteredEntities[type].Remove(entityId);
    }

    public void SendMessage(Guid senderId, Guid receiverId, double delayTime, int type, object extraInfo)
    {
        Message message;

        message = new Message(senderId, receiverId, type, delayTime, extraInfo);

        //If the message has no delay then call the delegate handler
        if (delayTime == 0)
        {
            if (RegisteredEntities[type] == null)
            {
                return;
            }

            if (RegisteredEntities[type][receiverId] == null)
            {
                return;
            }

            RegisteredEntities[type][receiverId](message);
        }

        //If the messaged was a delayed one, calculate its future time and put it in the message queue
        else
        {
            message.DispatchTime = DateTime.Now.Ticks + delayTime;
            MessageQueue.Enqueue(message);
        }
    }

    public void Update()
    {
        double currentTime = DateTime.Now.Ticks;

        while (MessageQueue.Count != 0 && MessageQueue.Peek().DispatchTime < currentTime)
        {
            var message = MessageQueue.Dequeue();

            if (RegisteredEntities[message.Type] == null)
            {
                return;
            }

            if (RegisteredEntities[message.Type][message.RecieverID] == null)
            {
                return;
            }

            RegisteredEntities[message.Type][message.RecieverID](message);
        }
    }

}