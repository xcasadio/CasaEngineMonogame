
using System;




namespace CasaEngine.AI.Messaging
{
    /// <summary>
    /// This interface indicates that the implementer is capable of handling messages
    /// </summary>
    public interface IMessageable
    {

        /// <summary>
        /// The message handler of the entity
        /// </summary>
        /// <param name="message">Message the entity will recieve</param>
        /// <returns>True if the entity could handle the message, false if not</returns>
        bool HandleMessage(Message message);

    }
}
