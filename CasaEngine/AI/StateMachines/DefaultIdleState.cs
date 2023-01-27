#region Using Directives

using System;


using CasaEngine.AI.Messaging;

#endregion

namespace CasaEngine.AI.StateMachines
{
	/// <summary>
	/// Default state that does nothing at all
	/// </summary>
	/// <typeparam name="T">The type of the entity that will hold the state machine</typeparam>
	[Serializable]
	public class DefaultIdleState<T> : IState<T> where T : /*BaseEntity,*/ IFSMCapable<T>
	{
		#region Methods

        /// <summary>
        /// The name of the state
        /// </summary>
        public string Name
        {
            get { return "DefaultIdleState"; }
        }

		/// <summary>
		/// The action that happens when the state is first entered
		/// </summary>
		/// <param name="entity">The entity that changed state</param>
		/// <remarks>Does nothing</remarks>
		public void Enter(T entity) {}

		/// <summary>
		/// The action that happens when the state finishes
		/// </summary>
		/// <param name="entity">The entity that changed state</param>
		/// <remarks>Does nothing</remarks>
		public void Exit(T entity)	{}

		/// <summary>
		/// The action executed everytime the state is update
		/// </summary>
		/// <param name="entity">The entity that changed state</param>
		/// <remarks>Does nothing</remarks>
        public void Update(T entity, float elpasedTime_) { }

		/// <summary>
		/// Function that will handle the messages sent to the state from the state machine
		/// </summary>
		/// <param name="entity">The entity that owns the state</param>
		/// <param name="message">Message sent to the entity</param>
		/// <returns>True if the state could handle the message, false if not</returns>
		/// <remarks>Returns false by default</remarks>
		public bool HandleMessage(T entity, Message message)
		{
			return false;
		}

		#endregion
	}
}
