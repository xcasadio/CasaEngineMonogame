using CasaEngineCommon.Collection;


namespace CasaEngine.AI.Messaging
{
    public sealed class MessageManagerHandler : IMessageManager
    {

        private static readonly MessageManagerHandler manager = new MessageManagerHandler();

        internal UniquePriorityQueue<Message> messageQueue;

        internal Dictionary<int, Dictionary<int, MessageHandlerDelegate>> registeredEntities;



        static MessageManagerHandler() { }

        private MessageManagerHandler()
        {
            messageQueue = new UniquePriorityQueue<Message>(new MessageComparer(1000));
            registeredEntities = new Dictionary<int, Dictionary<int, MessageHandlerDelegate>>();
        }



        public static MessageManagerHandler Instance => manager;


        public void ResetManager(double precision)
        {
            messageQueue = new UniquePriorityQueue<Message>(new MessageComparer(precision));
            registeredEntities = new Dictionary<int, Dictionary<int, MessageHandlerDelegate>>();
        }

        public void RegisterForMessage(int type, int entityID, MessageHandlerDelegate handler)
        {
            //If the table for this type of message didn´t exist we set it up and register ourselves
            if (registeredEntities[type] == null)
            {
                registeredEntities[type] = new Dictionary<int, MessageHandlerDelegate>();
                registeredEntities[type][entityID] = handler;
                return;
            }

            //If another handler existed it will be overriden
            registeredEntities[type][entityID] = handler;
        }

        public void UnregisterForMessage(int type, int entityID)
        {
            if (registeredEntities[type] == null)
                return;

            registeredEntities[type].Remove(entityID);
        }

        public void SendMessage(int senderID, int recieverID, double delayTime, int type, object extraInfo)
        {
            Message message;

            message = new Message(senderID, recieverID, type, delayTime, extraInfo);

            //If the message has no delay then call the delegate handler
            if (delayTime == 0)
            {
                if (registeredEntities[type] == null)
                    return;

                if (registeredEntities[type][recieverID] == null)
                    return;

                registeredEntities[type][recieverID](message);
            }

            //If the messaged was a delayed one, calculate its future time and put it in the message queue
            else
            {
                message.dispatchTime = System.DateTime.Now.Ticks + delayTime;
                messageQueue.Enqueue(message);
            }
        }

        public void Update()
        {
            Message message;
            double currentTime;

            currentTime = System.DateTime.Now.Ticks;

            while (messageQueue.Count != 0 && messageQueue.Peek().dispatchTime < currentTime)
            {
                message = messageQueue.Dequeue();

                if (registeredEntities[message.type] == null)
                    return;

                if (registeredEntities[message.type][message.recieverID] == null)
                    return;

                registeredEntities[message.type][message.recieverID](message);
            }
        }

    }
}
