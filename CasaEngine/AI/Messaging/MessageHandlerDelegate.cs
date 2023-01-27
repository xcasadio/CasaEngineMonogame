#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.Messaging
{
	/// <summary>
	/// Delegate for any function that can handle a message
	/// </summary>
	/// <param name="message">The message the delegate is going to handle</param>
	/// <returns>True if the handler could handle the message, false if not</returns>
	public delegate bool MessageHandlerDelegate(Message message);
}
