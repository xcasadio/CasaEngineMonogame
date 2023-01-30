using CasaEngineCommon.Collection;



namespace CasaEngine.AI.Messaging
{
    public sealed class MessageManagerRouter : IMessageManager
    {

        private static readonly MessageManagerRouter manager = new MessageManagerRouter();

        internal UniquePriorityQueue<Message> messageQueue;



        static MessageManagerRouter() { }

        private MessageManagerRouter()
        {
            messageQueue = new UniquePriorityQueue<Message>(new MessageComparer(1000));
        }



        public static MessageManagerRouter Instance => manager;


        public void ResetManager(double precision)
        {
            messageQueue = new UniquePriorityQueue<Message>(new MessageComparer(precision));
        }

        public void SendMessage(int senderID, int recieverID, double delayTime, int type, object extraInfo)
        {
            Message message;
            //IMessageable entity;

            message = new Message(senderID, recieverID, type, delayTime, extraInfo);

            //If the message has no delay then call the delegate handler
            if (delayTime == 0)
            {
                throw new NotImplementedException();
                //Test if the entity can handle the message
                //entity = EntityManager.Instance[recieverID] as IMessageable;
                /*if (entity == null)
					return;

				entity.HandleMessage(message);*/
            }

            //If the message was a delayed one, calculate its future time and put it in the message queue
            else
            {
                message.dispatchTime = System.DateTime.Now.Ticks + delayTime;
                messageQueue.Enqueue(message);
            }
        }

        public void Update()
        {
            double currentTime;
            Message message;
            //IMessageable entity;

            currentTime = System.DateTime.Now.Ticks;

            while (messageQueue.Count != 0 && messageQueue.Peek().dispatchTime < currentTime)
            {
                message = messageQueue.Dequeue();

                //Test if the entity can handle the message
                throw new NotImplementedException();
                //entity = EntityManager.Instance[message.recieverID] as IMessageable;
                /*if (entity == null)
					return;

				entity.HandleMessage(message);*/
            }
        }

    }
}
