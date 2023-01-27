#region Using Directives

using System;
using System.Collections.Generic;

using CasaEngine.AI.Graphs;

#endregion

namespace CasaEngine.AI.Pathfinding
{
	/// <summary>
	/// This interface defines an algorithm capable of searching in a graph (using full or time-sliced searchs)
	/// </summary>
	/// <typeparam name="T">The type of the edges of the graph</typeparam>
	public interface IGraphSearchAlgorithm<T>
		where T : Edge
	{
		#region Properties

		/// <summary>
		/// Gets the path found by the search algorithm as a list of node indexes
		/// </summary>
		List<int> PathOfNodes
		{
			get;
		}

		/// <summary>
		/// Gets the path found by the search algoritm as a list of edges
		/// </summary>
		List<T> PathOfEdges
		{
			get;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Initializes a search
		/// </summary>
		/// <param name="source">Source node</param>
		/// <param name="target">Target node</param>
		/// <remarks>Calling this method is mandatory when using time-sliced searchs</remarks>
		void Initialize(int source, int target);

		/// <summary>
		/// Searches in a graph
		/// </summary>
		/// <returns>The state of the search</returns>
		SearchState Search();

		/// <summary>
		/// Does one full iteration in the search process. Used in time-sliced searchs
		/// </summary>
		/// <returns>The state of the search</returns>
		SearchState CycleOnce();

		#endregion
	}
}
