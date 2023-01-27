#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.Pathfinding
{
	/// <summary>
	/// Indicates the state of a search
	/// </summary>
	public enum SearchState : int
	{
		/// <summary>
		/// The search was completed and the objective was found
		/// </summary>
		CompletedAndFound = 1,

		/// <summary>
		/// The search was completed but the objective wasn´t found
		/// </summary>
		CompletedAndNotFound = 2,

		/// <summary>
		/// The search has not been completed yet (time-sliced search)
		/// </summary>
		NotCompleted = 3
	}
}
