#region Using Directives

using System;
using System.Collections.Generic;

using CasaEngine.AI.Graphs;

#endregion

namespace CasaEngine.AI.Pathfinding
{
	/// <summary>
	/// This class represents the width first search algorithm for graphs. This algorithm searches in a graph
	/// going deeper and deeper in a branch till the branch ends. Then it backtracks and continue again. It´s not 
	/// a complete or optimal search (it may not find a solution and if it does, it can give a not optimal solution)
	/// </summary>
	/// <remarks>
	/// Based on Mat Buckland implementation from his book "Programming Game AI by Example"
	/// </remarks>
	/// <typeparam name="T">The type of the nodes</typeparam>
	/// <typeparam name="K">The type of the edges</typeparam>
	public class DepthFirstSearch<T, K> : GraphSearchAlgorithm<T, K>
		where T : Node
		where K : Edge
	{
		#region Fields

		/// <summary>
		/// List of visited nodes
		/// </summary>
		protected internal List<Visibility> visited;

		/// <summary>
		/// Stack of edges the algorithm is going to visit
		/// </summary>
		protected internal Stack<K> stackedEdges;

		/// <summary>
		/// The route from the source to the target as a list of node indexes
		/// </summary>
		protected internal List<int> route;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="graph">The graph where the search will be performed</param>
		public DepthFirstSearch(Graph<T, K> graph)
			: base(graph)
		{ }

		#endregion

		#region Properties

		/// <summary>
		/// Gets the path found by the search algorithm as a list of node indexes
		/// </summary>
		public override List<int> PathOfNodes
		{
			get
			{
				List<int> path;
				int node;

				//If the search didn´t find anything, return null
				if (found != SearchState.CompletedAndFound)
					return null;

				//Get the list of nodes from source to target. The route list is visited in reverse order
				path = new List<int>();
				node = target;

				while (node != source)
				{
					path.Insert(0, node);
					node = route[node];
				}

				return path;
			}
		}

		/// <summary>
		/// Gets the path found by the search algorithm as a list of edges
		/// </summary>
		public override List<K> PathOfEdges
		{
			get
			{
				List<K> path;
				List<int> nodes;

				//If the search didn´t find anything, return null
				if (found != SearchState.CompletedAndFound)
					return null;

				//Get the nodes that form the path
				nodes = PathOfNodes;

				//Get the edges between the nodes from the graph
				path = new List<K>();
				for (int i = 0; i < nodes.Count - 1; i++)
					path.Add(graph.GetEdge(nodes[i], nodes[i + 1]));

				return path;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Initializes a search
		/// </summary>
		/// <param name="source">Source node</param>
		/// <param name="target">Target node</param>
		/// <remarks>Calling this method is mandatory when using time-sliced searchs</remarks>
		public override void Initialize(int source, int target)
		{
			K dummy;

			base.Initialize(source, target);

			visited = new List<Visibility>();
			route = new List<int>();

			for (int i = 0; i < graph.NodeCount; i++)
			{
				visited.Add(Visibility.Unvisited);
				route.Add(Node.NoParent);
			}

			//Initialize the edges stack
			stackedEdges = new Stack<K>();

			//Create a dummy edge to start the search
			dummy = Activator.CreateInstance<K>();
			dummy.Start = source;
			dummy.End = source;
			stackedEdges.Push(dummy);
		}

		/// <summary>
		/// Searches in a graph
		/// </summary>
		/// <returns>The state of the search</returns>
		public override SearchState Search()
		{
			K next;
			List<K> nodeEdges;

			//While there are stacked edges
			while (stackedEdges.Count != 0)
			{
				next = stackedEdges.Pop();

				route[next.End] = next.Start;
				visited[next.End] = Visibility.Visited;

				//If the edge ends in the target, return success
				if (next.End == target)
				{
					found = SearchState.CompletedAndFound;
					return found;
				}

				//Stack all child edges from this node that aren´t visited
				nodeEdges = graph.GetEdgesFromNode(next.End);
				for (int i = 0; i < nodeEdges.Count; i++)
					if (visited[nodeEdges[i].End] == Visibility.Unvisited)
						stackedEdges.Push(nodeEdges[i]);
			}

			//The search failed
			found = SearchState.CompletedAndNotFound;
			return found;
		}

		/// <summary>
		/// Does one full iteration in the search process. Used in time-sliced searhs
		/// </summary>
		/// <returns>The state of the search</returns>
		public override SearchState CycleOnce()
		{
			K next;
			List<K> nodeEdges;

			//If the stack is empty, the search was a failure
			if (stackedEdges.Count == 0)
			{
				found = SearchState.CompletedAndNotFound;
				return found;
			}

			//Pop an edge
			next = stackedEdges.Pop();

			route[next.End] = next.Start;
			visited[next.End] = Visibility.Visited;

			//If the edge ends in the target, return success
			if (next.End == target)
			{
				found = SearchState.CompletedAndFound;
				return found;
			}

			//Stack all child edges from this node that aren´t visited
			nodeEdges = graph.GetEdgesFromNode(next.End);
			for (int i = 0; i < nodeEdges.Count; i++)
				if (visited[nodeEdges[i].End] == Visibility.Unvisited)
					stackedEdges.Push(nodeEdges[i]);

			found = SearchState.NotCompleted;
			return found;
		}

		#endregion
	}
}
