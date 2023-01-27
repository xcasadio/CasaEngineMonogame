#region Using Directives

using System;


using CasaEngine.AI.Messaging;

#endregion

namespace CasaEngine.AI.StateMachines
{
	/// <summary>
	/// This interface defines the minimum functionality expected from a Moore Finite State Machine
	/// </summary>
	/// <typeparam name="T">Type of the entity owner of the state machine</typeparam>
	public interface IFiniteStateMachine<T> : IMessageable where T : /*BaseEntity,*/ IFSMCapable<T>
	{
		#region Properties

		/// <summary>
		/// Gets or sets the current state
		/// </summary>
		IState<T> CurrentState
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the global state
		/// </summary>
		IState<T> GlobalState
		{
			get;
			set;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Updates the finite state machine
		/// </summary>
		void Update(float elpasedTime_);

		/// <summary>
		/// Changes the state of the state machine
		/// </summary>
		/// <param name="newState">The new state the machine is going to transition</param>
		void Transition(IState<T> newState);

		/// <summary>
		/// Puts the finite state machine back to its previous state
		/// </summary>
		void RevertStateChange();

		/// <summary>
		/// Tells if the state machine is in a determinated state or not
		/// </summary>
		/// <param name="state">The state to test</param>
		/// <returns>True if the machine is in state, false if it´s not</returns>
		bool IsInState(IState<T> state);

		#endregion
	}
}
