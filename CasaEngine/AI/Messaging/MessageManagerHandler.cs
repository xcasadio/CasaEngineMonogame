#region Using Directives

using System;
using System.Collections.Generic;

using CasaEngineCommon.Collection;

#endregion

namespace CasaEngine.AI.Messaging
{
	/// <summary>
	/// This class represents a message manager that handles messages through delegates. A function that wants to
	/// recieve a message with this system should register itself to listen messages using a delegate that
	/// will be registered in the message handler. The entity must register for every type of message it wants
	/// to recieve. The message manager is a thread-safe singleton
	/// </summary>
	/// <remarks>
	/// Based on Brian Schwab idea from his book "AI Game Engine Programming"
	/// </remarks>
	public sealed class MessageManagerHandler : IMessageManager
	{
		#region Fields

		/// <summary>
		/// Singleton instance. Lazy instantiation
		/// </summary>
		private static readonly MessageManagerHandler manager = new MessageManagerHandler();

		/// <summary>
		/// Queue with all the messages
		/// </summary>
		internal UniquePriorityQueue<Message> messageQueue;

		/// <summary>
		/// Table where the entities register their handler delegates
		/// </summary>
		internal Dictionary<int, Dictionary<int, MessageHandlerDelegate>> registeredEntities;

		#endregion

		#region Constructors

		/// <summary>
		/// Static constructor for thread-safe implementation
		/// </summary>
		static MessageManagerHandler() {}

		/// <summary>
		/// Default constructor
		/// </summary>
		private MessageManagerHandler()
		{
			messageQueue = new UniquePriorityQueue<Message>(new MessageComparer(1000));
			registeredEntities = new Dictionary<int, Dictionary<int, MessageHandlerDelegate>>();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the manager instance
		/// </summary>
		public static MessageManagerHandler Instance
		{
			get { return manager; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Resets the manager with a new precision time to discard messages
		/// </summary>
		/// <remarks>
		/// This method erases all info on the manager! Should be called carefully
		/// </remarks>
		/// <param name="precision">The precision time to decide if two messages are equal or not</param>
		public void ResetManager(double precision)
		{
			messageQueue = new UniquePriorityQueue<Message>(new MessageComparer(precision));
			registeredEntities = new Dictionary<int, Dictionary<int, MessageHandlerDelegate>>();
		}

		/// <summary>
		/// Registers an entity for a message type
		/// </summary>
		/// <param name="type">Message type the entity is going to register</param>
		/// <param name="entityID">The entity that its registering</param>
		/// <param name="handler">The handler delegate that should be called when a message arrives for this entity</param>
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

		/// <summary>
		/// Unregisters an entity for a message type
		/// </summary>
		/// <param name="type">The message type the entity wants to unregister</param>
		/// <param name="entityID">The entity that its unregistering</param>
		public void UnregisterForMessage(int type, int entityID)
		{
			if (registeredEntities[type] == null)
				return;

			registeredEntities[type].Remove(entityID);
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

		/// <summary>
		/// Updates the message handler and sends any messages than need to be sent
		/// </summary>
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

		#endregion
	}
}
