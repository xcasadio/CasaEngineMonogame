#region Using Directives

using System;


using CasaEngine.AI.Messaging;

#endregion

namespace CasaEngine.AI.Goals
{
	/// <summary>
	/// This class represents an atomic goal
	/// </summary>
	/// <typeparam name="T">The BaseEntity type capable of handling the goal</typeparam>
	[Serializable]
	public abstract class Goal<T> : IMessageable where T : BaseEntity
	{
		#region Fields

		/// <summary>
		/// The entity owner of the goal
		/// </summary>
		protected internal T owner;

		/// <summary>
		/// The status of the goal
		/// </summary>
		protected internal GoalProcessingState status;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="owner">The ownwer of the goal</param>
		/// <remarks>All goals start in the inactive state</remarks>
		public Goal(T owner)
		{
			this.owner = owner;
			this.status = GoalProcessingState.Inactive;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the goal state
		/// </summary>
		public GoalProcessingState Status
		{
			get
			{
				return status;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Activates the goal
		/// </summary>
		public abstract void Activate();
		
		/// <summary>
		/// Processes the goal. This method is called every update step of the goal until it´s finished
		/// </summary>
		/// <returns>The new state of the goal after processing it</returns>
		public abstract GoalProcessingState Process();
		
		/// <summary>
		/// Finishes the goal
		/// </summary>
		public abstract void Terminate();

		/// <summary>
		/// Inserts a new subgoal at the start of the goal list
		/// </summary>
		/// <param name="goal">The new subgoal to insert</param>
		/// <remarks>Throws an exception by default (this method is used in composite goals)</remarks>
		public virtual void AddFrontSubgoal(Goal<T> goal)
		{
			throw new InvalidOperationException("Can´t insert a subgoal in an atomic goal");
		}

		/// <summary>
		/// Queues a new subgoal at the end of the goal list
		/// </summary>
		/// <param name="goal">The new subgoal to queue</param>
		/// <remarks>Throws an exception by default (this method is used in composite goals)</remarks>
		public virtual void AddRearSubgoal(Goal<T> goal)
		{
			throw new InvalidOperationException("Can´t queue a subgoal in an atomic goal");
		}

		/// <summary>
		/// Handles messages sent to the goal
		/// </summary>
		/// <param name="message">The message sent to the goal</param>
		/// <returns>True if the goal could manage the message, false otherwise</returns>
		/// <remarks>Returns false by default</remarks>
		public virtual bool HandleMessage(Message message)
		{
			return false;
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Activates a goal if it´s state is Inactive
		/// </summary>
		protected void ActivateIfInactive()
		{
			if (status == GoalProcessingState.Inactive)
				Activate();
		}

		/// <summary>
		/// Sets the goal to inactive if it´s state is Failed
		/// </summary>
		protected void ReactivateIfFailed()
		{
			if (status == GoalProcessingState.Failed)
				status = GoalProcessingState.Inactive;
		}

		#endregion
	}
}
