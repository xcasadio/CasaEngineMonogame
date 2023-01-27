#region Using Directives

using System;
using System.Collections.Generic;
using CasaEngineCommon.Collection;


#endregion

namespace CasaEngine.AI.Messaging
{
	/// <summary>
	/// This class represents a message manager that routes messages to entities. Reciving entities should implement 
	/// the IMessageable interface to handle the message. The message manager is a thread-safe singleton
	/// </summary>
	/// <remarks>
	/// Based on Mat Buckland implementation from his book "Programming Game AI by Example"
	/// </remarks>
	public sealed class MessageManagerRouter : IMessageManager
	{
		#region Fields

		/// <summary>
		/// Singleton instance. Lazy instantiation
		/// </summary>
		private static readonly MessageManagerRouter manager = new MessageManagerRouter();

		/// <summary>
		/// Queue with all the messages
		/// </summary>
		internal UniquePriorityQueue<Message> messageQueue;

		#endregion

		#region Constructors

		/// <summary>
		/// Static constructor for thread-safe implementation
		/// </summary>
		static MessageManagerRouter() { }

		/// <summary>
		/// Default constructor
		/// </summary>
		private MessageManagerRouter()
		{
			messageQueue = new UniquePriorityQueue<Message>(new MessageComparer(1000));
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the manager instance
		/// </summary>
		public static MessageManagerRouter Instance
		{
			get { return manager; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Resets the manager with a new precision time to discard messages
		/// </summary>
		/// <remarks>
		/// This method erases all messages on the manager! Should be called carefully
		/// </remarks>
		/// <param name="precision">The precision time to decide if two messages are equal or not</param>
		public void ResetManager(double precision)
		{
			messageQueue = new UniquePriorityQueue<Message>(new MessageComparer(precision));
		}

		/// <summary>
		/// Sends a message from one entity to another
		/// </summary>
		/// <param name="senderID">The sender ID</param>
		/// <param name="recieverID">The reciever ID</param>
		/// <param name="delayTime">The delay time of the message in ticks</param>
		/// <param name="type">The message type</param>
		/// <param name="extraInfo">Extra info for the message</param>
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

		/// <summary>
		/// Updates the message handler and sends any messages than need to be sent
		/// </summary>
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

		#endregion
	}
}
