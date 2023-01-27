#region Using Directives

using System;


using CasaEngine.AI.Messaging;

#endregion

namespace CasaEngine.AI.StateMachines
{
	/// <summary>
	/// This interface represents the basic functionality for a State that can operate in a State Machine
	/// </summary>
	/// <remarks>
	/// Based on the implementation of Mat Buckland given in his book "Programming Game AI By Example"
	/// </remarks>
	/// <typeparam name="T">The type of the entity that will hold the state machine</typeparam>
	public interface IState<T> where T : /*BaseEntity,*/ IFSMCapable<T>
	{
		#region Methods

        /// <summary>
        /// The name of the state
        /// </summary>
        string Name { get; }

		/// <summary>
		/// The action that happens when the state is first entered
		/// </summary>
		/// <param name="entity">The entity that changed state</param>
		void Enter(T entity);

		/// <summary>
		/// The action that happens when the state finishes
		/// </summary>
		/// <param name="entity">The entity that changed state</param>
		void Exit(T entity);

		/// <summary>
		/// The action executed everytime the state is update
		/// </summary>
		/// <param name="entity">The entity that changed state</param>
		void Update(T entity, float elpasedTime_);

		/// <summary>
		/// Function that will handle the messages sent to the state from the state machine
		/// </summary>
		/// <param name="entity">The entity that owns the state</param>
		/// <param name="message">Message sent to the entity</param>
		/// <returns>True if the state could handle the message, false if not</returns>
		bool HandleMessage(T entity, Message message);

		#endregion
	}
}
