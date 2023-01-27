#region Using Directives

using System;


using CasaEngine.AI.Messaging;
using CasaEngineCommon.Logger;
using CasaEngine.Gameplay.Actor.Object;

#endregion

namespace CasaEngine.AI.StateMachines
{
	/// <summary>
	/// This class represents a classical Moore Finite State Machine (FSM). The FSM is the one that orchestrates
	/// all state transitions and executes them when the game logic updates the FSM
	/// </summary>
	/// <remarks>
	/// Based on the implementation of Mat Buckland from his book "Programming Game AI By Example"
	/// </remarks>
	/// <typeparam name="T">Type of the entity owner of the state machine</typeparam>
	[Serializable]
	public class FiniteStateMachine<T> : IFiniteStateMachine<T> where T : /*BaseEntity,*/ IFSMCapable<T>
	{
		#region Fields

		/// <summary>
		/// The owner of the state machine
		/// </summary>
		protected internal T owner;

		/// <summary>
		/// Current state of the state machine
		/// </summary>
		protected internal IState<T> currentState;

		/// <summary>
		/// Previous state of the state machine. Gives the machine a short time memory
		/// </summary>
		protected internal IState<T> previousState;

		/// <summary>
		/// Global state of the state machine
		/// </summary>
		protected internal IState<T> globalState;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor. The state machine starts with DefaultIdleStates
		/// </summary>
		/// <param name="owner">The entity that owns the state machine</param>
		public FiniteStateMachine(T owner)
		{
			this.owner = owner;
			
			currentState = new DefaultIdleState<T>();
            previousState = new DefaultIdleState<T>();
            globalState = new DefaultIdleState<T>();
		}

		/// <summary>
		/// Creates a finite state machine with initial states
		/// </summary>
		/// <param name="owner">The entity that owns the state machine</param>
		/// <param name="currentState">The starting state of the machine</param>
		/// <param name="globalState">The global state of the machine</param>
		public FiniteStateMachine(T owner, IState<T> currentState, IState<T> globalState)
		{
			this.owner = owner;			
			
			this.currentState = currentState;
			this.globalState = globalState;
            this.previousState = new DefaultIdleState<T>();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the current state
		/// </summary>
		public IState<T> CurrentState
		{
			get { return currentState; }
			set { this.currentState = value; }
		}

		/// <summary>
		/// Gets or sets the global state
		/// </summary>
		public IState<T> GlobalState
		{
			get { return globalState; }
			set { this.globalState = value; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Updates the finite state machine
		/// </summary>
		public void Update(float elpasedTime_)
		{
            globalState.Update(owner, elpasedTime_);
            currentState.Update(owner, elpasedTime_);
		}

		/// <summary>
		/// Changes the state of the finite state machine
		/// </summary>
		/// <param name="newState">The new state the machine is going to transition</param>
		public void Transition(IState<T> newState)
		{
			/*String message = String.Empty;

            if (LogManager.Instance.Verbosity == LogManager.LogVerbosity.Debug)
            {
                string n;

                if (owner is BaseObject)
                {
                    n = (owner as BaseObject).Name;
                }
                else
                {
                    n = "unknown";
                }

                if (LogManager.Instance.Verbosity == LogManager.LogVerbosity.Debug)
                {
                    LogManager.Instance.WriteLineDebug("Object '" + n + "' change state '" + currentState.Name + "' to '" + newState.Name + "'");
                }
            }*/

			//Exit the actual state
            currentState.Exit(owner);

			//Actualize internal values
			previousState = currentState;
			currentState = newState;

			//Enter the new state
			currentState.Enter(owner);
		}

		/// <summary>
		/// Puts the finite state machine back to its previous state
		/// </summary>
		public void RevertStateChange()
		{
			Transition(previousState);
		}

		/// <summary>
		/// Tells if the state machine is in a determinated state or not
		/// </summary>
		/// <param name="state">The state we want to check</param>
		/// <returns>True if the machine is in state, false if it´s not</returns>
		public bool IsInState(IState<T> state)
		{
			if (this.currentState == state)
				return true;

			else
				return false;
		}

		/// <summary>
		/// The message handler of the Finite State Machine
		/// </summary>
		/// <param name="message">Message the entity will recieve</param>
		/// <returns>True if the entity could handle the message, false if not</returns>
		public bool HandleMessage(Message message)
		{
			//Try to handle the message with the current state
			if (currentState.HandleMessage(owner, message) == true)
				return true;

			//If the current state couldn´t handle the message, try the global state
			if (globalState.HandleMessage(owner, message) == true)
				return true;

			//The machine wasn´t able to handle the message
			return false;
		}

		#endregion
	}
}
