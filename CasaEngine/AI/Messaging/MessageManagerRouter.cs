using CasaEngineCommon.Collection;



namespace CasaEngine.AI.Messaging
{
    public sealed class MessageManagerRouter : IMessageManager
    {

        private static readonly MessageManagerRouter Manager = new();

        internal UniquePriorityQueue<Message> MessageQueue;



        static MessageManagerRouter() { }

        private MessageManagerRouter()
        {
            MessageQueue = new UniquePriorityQueue<Message>(new MessageComparer(1000));
        }



        public static MessageManagerRouter Instance => Manager;


        public void ResetManager(double precision)
        {
            MessageQueue = new UniquePriorityQueue<Message>(new MessageComparer(precision));
        }

        public void SendMessage(int senderId, int recieverId, double delayTime, int type, object extraInfo)
        {
            Message message;
            //IMessageable entity;

            message = new Message(senderId, recieverId, type, delayTime, extraInfo);

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
                message.DispatchTime = DateTime.Now.Ticks + delayTime;
                MessageQueue.Enqueue(message);
            }
        }

        public void Update()
        {
            double currentTime;
            Message message;
            //IMessageable entity;

            currentTime = DateTime.Now.Ticks;

            while (MessageQueue.Count != 0 && MessageQueue.Peek().DispatchTime < currentTime)
            {
                message = MessageQueue.Dequeue();

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
