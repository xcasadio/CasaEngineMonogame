#region Using Directives

using System;
using System.Collections.Generic;


using CasaEngine.AI.Graphs;


#endregion

namespace CasaEngine.AI.Pathfinding
{
	/// <summary>
	/// Abstract base class for all graph search algorithms
	/// </summary>
	/// <typeparam name="T">The type of the nodes</typeparam>
	/// <typeparam name="K">The type of the edges</typeparam>
	public abstract class GraphSearchAlgorithm<T,K> : IGraphSearchAlgorithm<K>
		where T : Node
		where K : Edge
	{
		#region Fields

		/// <summary>
		/// Graph where the search will be performed
		/// </summary>
		protected internal Graph<T, K> graph;

		/// <summary>
		/// The source target node index
		/// </summary>
		protected internal int source;

		/// <summary>
		/// The target node index
		/// </summary>
		protected internal int target;

		/// <summary>
		/// The state of the search
		/// </summary>
		protected internal SearchState found;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="graph">The graph where the search will be performed</param>
		protected GraphSearchAlgorithm(Graph<T, K> graph)
		{
			String message = String.Empty;

			if (ValidateGraph(graph, ref message) == false)
				throw new AIException("graph", this.GetType().ToString(), message);

			this.graph = graph;
			this.found = SearchState.NotCompleted;
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the graph value is correct (!= null)
		/// </summary>
		/// <param name="graph">The graph value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateGraph(Graph<T, K> graph, ref string message)
		{
			if (graph == null)
			{
				message = "The graph where the search will be performed can´t be null";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Validates if the node index value is correct (>= 0)
		/// </summary>
		/// <param name="index">The node index value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateNode(int index, ref string message)
		{
			if (index < 0)
			{
				message = "The node index can´t be lower than 0";
				return false;
			}

			return true;
		}

		#endregion

		#region IGraphSearchAlgorithm Properties

		/// <summary>
		/// Gets the path found by the search algorithm as a list of node indexes
		/// </summary>
		public abstract List<int> PathOfNodes
		{
			get;
		}

		/// <summary>
		/// Gets the path found by the search algoritm as a list of edges
		/// </summary>
		public abstract List<K> PathOfEdges
		{
			get;
		}

		#endregion

		#region IGraphSearchAlgorithm Methods

		/// <summary>
		/// Initializes a search
		/// </summary>
		/// <param name="source">Source node</param>
		/// <param name="target">Target node</param>
		/// <remarks>Calling this method is mandatory when using time-sliced searchs</remarks>
		public virtual void Initialize(int source, int target)
		{
			String message = String.Empty;

			if (ValidateNode(source, ref message) == false)
				throw new AIException("source", this.GetType().ToString(), message);

			if (ValidateNode(target, ref message) == false)
				throw new AIException("target", this.GetType().ToString(), message);

			this.source = source;
			this.target = target;
			this.found = SearchState.NotCompleted;
		}

		/// <summary>
		/// Searches in a graph
		/// </summary>
		/// <returns>The state of the search</returns>
		public abstract SearchState Search();

		/// <summary>
		/// Does one full iteration in the search process. Used in time-sliced searhs
		/// </summary>
		/// <returns>The state of the search</returns>
		public abstract SearchState CycleOnce();

		#endregion
	}
}
