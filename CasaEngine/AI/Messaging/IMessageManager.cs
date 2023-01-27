#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.Messaging
{
	/// <summary>
	/// Common interface for all message managers
	/// </summary>
	public interface IMessageManager
	{
		#region Methods

		/// <summary>
		/// Resets the manager with a new precision time to discard messages
		/// </summary>
		/// <remarks>
		/// This method erases all messages on the manager! Should be called carefully
		/// </remarks>
		/// <param name="precision">The precision time to decide if two messages are equal or not</param>
		void ResetManager(double precision);

		/// <summary>
		/// Sends a message from one entity to another
		/// </summary>
		/// <param name="senderID">The sender ID</param>
		/// <param name="recieverID">The reciever ID</param>
		/// <param name="delayTime">The delay time of the message in ticks</param>
		/// <param name="type">The message type</param>
		/// <param name="extraInfo">Extra info for the message</param>
		void SendMessage(int senderID, int recieverID, double delayTime, int type, object extraInfo);

		/// <summary>
		/// Updates the message handler and sends any messages than need to be sent
		/// </summary>
		void Update();

		#endregion
	}
}
