#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.Pathfinding
{
	/// <summary>
	/// Indicates whether a node has been visited by the search algorithm or not
	/// </summary>
	public enum Visibility : int
	{
		/// <summary>
		/// The node has not been visited by the algorithm yet
		/// </summary>
		Unvisited = 1,

		/// <summary>
		/// The node has been visited by the algorithm
		/// </summary>
		Visited = 2
	}
}
