#region Using Directives

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Graphs
{
	/// <summary>
	/// This class has several example heuristics to be used in the A* search
	/// </summary>
	/// <typeparam name="T">The node type. Must be a NavigationNode</typeparam>
	/// <typeparam name="K">The edge type. Must be a WeightedEdge</typeparam>
	public static class Heuristics<T, K>
		where T : NavigationNode
		where K : WeightedEdge
	{
		/// <summary>
		/// Euclidean distance heuristic
		/// </summary>
		/// <param name="graph">The graph where we are doing the search</param>
		/// <param name="node1">The first node to calculate the heuristic (usually the start node)</param>
		/// <param name="node2">The secon node to calculate the heuristic (usually the end node)</param>
		/// <returns>The euclidean distance from node1 to node2 in the graph</returns>
		public static double EuclideanDistance(Graph<T, K> graph, int node1, int node2)
		{
			return (graph.GetNode(node1).Position - graph.GetNode(node2).Position).LengthSquared();
		}

		/// <summary>
		/// Manhattan distance heuristic
		/// </summary>
		/// <param name="graph">The graph where we are doing the search</param>
		/// <param name="node1">The first node to calculate the heuristic (usually the start node)</param>
		/// <param name="node2">The secon node to calculate the heuristic (usually the end node)</param>
		/// <returns>The manhattan distance from node1 to node2 in the graph</returns>
		public static double ManhattanDistance(Graph<T, K> graph, int node1, int node2)
		{
			Vector3 aux;

			aux = graph.GetNode(node1).Position - graph.GetNode(node2).Position;
			return System.Math.Abs(aux.X) + System.Math.Abs(aux.Y) + System.Math.Abs(aux.Z);
		}

		/// <summary>
		/// Djisktra distance heuristic
		/// </summary>
		/// <param name="graph">The graph where we are doing the search</param>
		/// <param name="node1">The first node to calculate the heuristic (usually the start node)</param>
		/// <param name="node2">The secon node to calculate the heuristic (usually the end node)</param>
		/// <returns>The djisktra distance from node1 to node2 in the graph. This distance is always 0</returns>
		public static double DjisktraDistance(Graph<T, K> graph, int node1, int node2)
		{
			return 0;
		}
	}
}
