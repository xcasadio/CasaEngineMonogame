#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.Goals
{
	/// <summary>
	/// State of the goal
	/// </summary>
	public enum GoalProcessingState
	{
		/// <summary>
		/// The goal is inactive
		/// </summary>
		Inactive = 0,

		/// <summary>
		/// The goal is active
		/// </summary>
		Active = 1,

		/// <summary>
		/// The goal has ended correctly
		/// </summary>
		Completed = 2,

		/// <summary>
		/// The goal has ended wrongly
		/// </summary>
		Failed = 3
	}
}
