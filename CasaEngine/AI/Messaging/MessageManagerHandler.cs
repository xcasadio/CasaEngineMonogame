using CasaEngineCommon.Collection;


namespace CasaEngine.AI.Messaging
{
    public sealed class MessageManagerHandler : IMessageManager
    {

        private static readonly MessageManagerHandler Manager = new();

        internal UniquePriorityQueue<Message> MessageQueue;

        internal Dictionary<int, Dictionary<int, MessageHandlerDelegate>> RegisteredEntities;



        static MessageManagerHandler() { }

        private MessageManagerHandler()
        {
            MessageQueue = new UniquePriorityQueue<Message>(new MessageComparer(1000));
            RegisteredEntities = new Dictionary<int, Dictionary<int, MessageHandlerDelegate>>();
        }



        public static MessageManagerHandler Instance => Manager;


        public void ResetManager(double precision)
        {
            MessageQueue = new UniquePriorityQueue<Message>(new MessageComparer(precision));
            RegisteredEntities = new Dictionary<int, Dictionary<int, MessageHandlerDelegate>>();
        }

        public void RegisterForMessage(int type, int entityId, MessageHandlerDelegate handler)
        {
            //If the table for this type of message didn´t exist we set it up and register ourselves
            if (RegisteredEntities[type] == null)
            {
                RegisteredEntities[type] = new Dictionary<int, MessageHandlerDelegate>();
                RegisteredEntities[type][entityId] = handler;
                return;
            }

            //If another handler existed it will be overriden
            RegisteredEntities[type][entityId] = handler;
        }

        public void UnregisterForMessage(int type, int entityId)
        {
            if (RegisteredEntities[type] == null)
            {
                return;
            }

            RegisteredEntities[type].Remove(entityId);
        }

        public void SendMessage(int senderId, int recieverId, double delayTime, int type, object extraInfo)
        {
            Message message;

            message = new Message(senderId, recieverId, type, delayTime, extraInfo);

            //If the message has no delay then call the delegate handler
            if (delayTime == 0)
            {
                if (RegisteredEntities[type] == null)
                {
                    return;
                }

                if (RegisteredEntities[type][recieverId] == null)
                {
                    return;
                }

                RegisteredEntities[type][recieverId](message);
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
            Message message;
            double currentTime;

            currentTime = DateTime.Now.Ticks;

            while (MessageQueue.Count != 0 && MessageQueue.Peek().DispatchTime < currentTime)
            {
                message = MessageQueue.Dequeue();

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
}
