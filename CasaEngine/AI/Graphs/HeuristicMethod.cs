#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace CasaEngine.AI.Graphs
{
	/// <summary>
	/// This delegate represents an heuristic distanace function for the A* search
	/// </summary>
	/// <typeparam name="T">The node type. Must be a NavigationNode</typeparam>
	/// <typeparam name="K">The edge type. Must be a WeightedEdge</typeparam>
	/// <param name="graph">The graph where we are doing the search</param>
	/// <param name="node1">The first node to calculate the heuristic (usually the start node)</param>
	/// <param name="node2">The secon node to calculate the heuristic (usually the end node)</param>
	/// <returns>The heuristic distance from node1 to node2 in the graph</returns>
	public delegate double HeuristicMethod<T, K>(Graph<T, K> graph, int node1, int node2) where T : NavigationNode where K : WeightedEdge;
}
