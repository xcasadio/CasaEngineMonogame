
using System;




namespace CasaEngine.AI.Messaging
{
    /// <summary>
    /// The type of the message
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Default message type
        /// </summary>
        DefaultMessage,


        /// <summary>
        /// Message indicating that a path was found
        /// </summary>
        PathReady,

        /// <summary>
        /// Message indicating that a path couldn´t be found
        /// </summary>
        PathNotAvailable,



        /// <summary>
        /// Message indicating that the animation of an actor change
        /// </summary>
        AnimationChanged,



        /// <summary>
        /// Collisions between sprite collisions object defense/attack (see Collision.Flag)
        /// </summary>
        Hit,

        /// <summary>
        /// Collisions between sprite collisions object attack/defense (see Collision.Flag)
        /// </summary>
        IHitSomeone,

    }
}
