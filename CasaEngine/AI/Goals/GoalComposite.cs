#region Using Directives

using System;
using System.Collections.Generic;


using CasaEngine.AI.Messaging;

#endregion

namespace CasaEngine.AI.Goals
{
	/// <summary>
	/// This class represents a composite goal
	/// </summary>
	/// <typeparam name="T">The BaseEntity type capable of handling the goa</typeparam>
	[Serializable]
	public abstract class GoalComposite<T> : Goal<T> where T : BaseEntity
	{
		#region Fields

		/// <summary>
		/// List of subgoals of this goal
		/// </summary>
		protected internal List<Goal<T>> subGoals;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="owner">The owner of this goal</param>
		public GoalComposite(T owner)
			: base(owner)
		{
			this.subGoals = new List<Goal<T>>();
		}

		#endregion

		#region Methods

		/// <summary>
		/// Finishes the goal
		/// </summary>
		/// <remarks>Terminates all the subgoals of this goal</remarks>
		public override void Terminate()
		{
			RemoveAllSubgoals();
		}

		/// <summary>
		/// Inserts a new subgoal at the start of the goal list
		/// </summary>
		/// <param name="goal">The new subgoal to insert</param>
		public override void AddFrontSubgoal(Goal<T> goal)
		{
			subGoals.Insert(0, goal);
		}

		/// <summary>
		/// Queues a new subgoal at the end of the goal list
		/// </summary>
		/// <param name="goal">The new subgoal to queue</param>
		public override void AddRearSubgoal(Goal<T> goal)
		{
			subGoals.Add(goal);
		}

		/// <summary>
		/// Handles messages sent to the goal
		/// </summary>
		/// <param name="message">The message sent to the goal</param>
		/// <returns>True if the goal could manage the message, false otherwise</returns>
		/// <remarks>By default the composite goal asks its first subgoal to handle the message</remarks>
		public override bool HandleMessage(Message message)
		{
			return ForwardMessage(message);
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Handles the processing of the subgoals of the composite goal
		/// </summary>
		/// <returns>The current status of the composite goal based on the status of its subgoals</returns>
		protected GoalProcessingState ProcessSubgoals()
		{
			GoalProcessingState subGoalStatus;

			//Remove the Completed or Failed goals from the goals list
			while (subGoals.Count != 0 && (subGoals[0].Status == GoalProcessingState.Completed || subGoals[0].Status == GoalProcessingState.Failed))
			{
				subGoals[0].Terminate();
				subGoals.RemoveAt(0);
			}

			//If there are goals left, process the first one
			if (subGoals.Count != 0)
			{
				//Process the first subgoal
				subGoalStatus = subGoals[0].Process();

				//If the goal was completed, but there are goals left, the composite goal continues to be active
				if (subGoalStatus == GoalProcessingState.Completed && subGoals.Count > 1)
					return GoalProcessingState.Active;

				//If not, return the status of the goal
				return subGoalStatus;
			}

			//There aren´t any goals left
			return GoalProcessingState.Completed;
		}

		/// <summary>
		/// Terminates all the goals in the subgoal list
		/// </summary>
		protected void RemoveAllSubgoals()
		{
			for (int i = 0; i < subGoals.Count; i++)
				subGoals[i].Terminate();

			subGoals.Clear();
		}

		/// <summary>
		/// Forwards the message to the first subgoal
		/// </summary>
		/// <param name="message">The message to handle</param>
		/// <returns>True if the message was handled, false if otherwise</returns>
		protected bool ForwardMessage(Message message)
		{
			if (subGoals.Count != 0)
				return subGoals[0].HandleMessage(message);

			return false;
		}

		#endregion
	}
}
