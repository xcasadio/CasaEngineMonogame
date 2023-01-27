#region Using Directives

using System;
using System.Collections.Generic;

using CasaEngineCommon.Collection;

using CasaEngine.AI.Graphs;

#endregion

namespace CasaEngine.AI.Pathfinding
{
	/// <summary>
	/// Class that implements the A* search algorithm. This algorithm is optimal and complete (it 
	/// finds a solution if it exists, and it´s always the best solution). Also, no other algorithm
	/// will visit less nodes than A*. All of this is true only if the heuristic function is monotonous
	/// (the heuristic function estimates the cost to reach a node). Euclidean and Manhattan distance
	/// heuristics satisfy this requeriment
	/// </summary>
	/// <remarks>
	/// Based on Mat Buckland implementation from his book "Programming Game AI by Example"
	/// </remarks>
	/// <typeparam name="T">The type of the nodes</typeparam>
	/// <typeparam name="K">The type of the edges</typeparam>
	public class AStarSearch<T, K> : GraphSearchAlgorithm<T, K>
		where T : NavigationNode
		where K : WeightedEdge
	{
		#region Fields

		/// <summary>
		/// Total cost to reach a node  (total cost = real cost + estimated cost)
		/// </summary>
		protected internal List<double> fCosts;

		/// <summary>
		/// Real cost to get to a node
		/// </summary>
		protected internal List<double> gCosts;

		/// <summary>
		/// Sub-graph with the shortest way to reach every node
		/// </summary>
		protected internal List<K> shortestPathTree;

		/// <summary>
		/// Nodes that the algorithm will visit to add to the shortest path tree
		/// </summary>
		protected internal List<K> frontier;

		/// <summary>
		/// Estimation function used by A* to aproximate the cost to a node
		/// </summary>
		protected internal HeuristicMethod<T, K> heuristic;

		/// <summary>
		/// Indexed priority queue to order the nodes for their total cost
		/// </summary>
		protected internal IndexedPriorityQueue<double> queue;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="graph">The graph where the search will be performed</param>
		/// <param name="heuristic">The heuristic function that will guide A*</param>
		public AStarSearch(Graph<T, K> graph, HeuristicMethod<T, K> heuristic) : base(graph)
		{
			this.heuristic = heuristic;
		}

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
				int aux;

				//If the search didn´t find anything, return an empty path
				path = new List<int>();
				if (found != SearchState.CompletedAndFound)
					return path;

				//Get the list of nodes from source to target. The shortest patrh tree is visited in reverse order
				path.Insert(0, target);
				aux = target;
				while (aux != source)
				{
					aux = shortestPathTree[aux].Start;
					path.Insert(0, aux);
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
				int aux;

				//If the search didn´t find anything, return an empty path
				path = new List<K>();
				if (found != SearchState.CompletedAndFound)
					return path;

				//Get the list of edges from source to target. The shortest patrh tree is visited in reverse order
				aux = target;
				while (aux != source)
				{
					path.Insert(0, shortestPathTree[aux]);
					aux = shortestPathTree[aux].start;
				}

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
			//Create the auxiliary lists
			fCosts = new List<double>(graph.NodeCount);
			gCosts = new List<double>(graph.NodeCount);
			shortestPathTree = new List<K>(graph.NodeCount);
			frontier = new List<K>(graph.NodeCount);

			//Initialize the values
			for (int i = 0; i < graph.NodeCount; i++)
			{
				fCosts.Add(0.0);
				gCosts.Add(0.0);
				shortestPathTree.Add(default(K));
				frontier.Add(default(K));
			}

			//Initialize the indexed priority queue and enqueue the start node
			queue = new IndexedPriorityQueue<double>(fCosts);
			queue.Enqueue(source);

			//Initialize the base
			base.Initialize(source, target);
		}

		/// <summary>
		/// Searches in a graph
		/// </summary>
		/// <returns>The state of the search</returns>
		public override SearchState Search()
		{
			int closestNode;
			List<K> edges;
			double hCost, gCost;

			while (queue.Count != 0)
			{
				//Dequeue the first node (the one with the lower cost) and add it to the shortest path tree
				closestNode = queue.Dequeue();
				shortestPathTree[closestNode] = frontier[closestNode];

				//If it´s the target, return
				if (closestNode == target)
				{
					found = SearchState.CompletedAndFound;
					return found;
				}

				//Get all edges that start from the current node
				edges = graph.GetEdgesFromNode(closestNode);
				for (int i = 0; i < edges.Count; i++)
				{
					//Calculate the estimated cost to reach the target from the next node
					hCost = heuristic(graph, target, edges[i].End);

					//Calculate the cost to reach the next node with the current one and the edge
					gCost = gCosts[closestNode] + edges[i].Cost;

					//If the node wasn´t in the frontier, add it
					if (frontier[edges[i].End] == null)
					{
						fCosts[edges[i].End] = gCost + hCost;
						gCosts[edges[i].End] = gCost;

						queue.Enqueue(edges[i].End);
						frontier[edges[i].End] = edges[i];
					}

					else //If it was, see if the new way of reaching the node is shorter or not. If it´s shorter, update the cost to reach it
					{
						if ((gCost < gCosts[edges[i].End]) && (shortestPathTree[edges[i].End] == null))
						{
							fCosts[edges[i].End] = gCost + hCost;
							gCosts[edges[i].End] = gCost;
							
							queue.ChangePriority(edges[i].End);
							frontier[edges[i].End] = edges[i];
						}
					}
				}
			}

			found = SearchState.CompletedAndNotFound;
			return found;
		}

		/// <summary>
		/// Does one full iteration in the search process. Used in time-sliced searchs
		/// </summary>
		/// <returns>The state of the search</returns>
		public override SearchState CycleOnce()
		{
			int closestNode;
			List<K> edges;
			double hCost, gCost;

			if (queue.Count == 0)
			{
				found = SearchState.CompletedAndNotFound;
				return found;
			}

			//Dequeue the first node (the one with the lower cost) and add it to the shortest path tree
			closestNode = queue.Dequeue();
			shortestPathTree[closestNode] = frontier[closestNode];

			//If it´s the target, return
			if (closestNode == target)
			{
				found = SearchState.CompletedAndFound;
				return found;
			}

			//Get all edges that start from the current node
			edges = graph.GetEdgesFromNode(closestNode);
			for (int i = 0; i < edges.Count; i++)
			{
				//Calculate the estimated cost to reach the target from the next node
				hCost = heuristic(graph, target, edges[i].End);

				//Calculate the cost to reach the next node with the current one and the edge
				gCost = gCosts[closestNode] + edges[i].Cost;

				//If the node wasn´t in the frontier, add it
				if (frontier[edges[i].End] == null)
				{
					fCosts[edges[i].End] = gCost + hCost;
					gCosts[edges[i].End] = gCost;

					queue.Enqueue(edges[i].End);
					frontier[edges[i].End] = edges[i];
				}

				else //If it was, see if the new way of reaching the node is shorter or not. If it´s shorter, update the cost to reach it
				{
					if ((gCost < gCosts[edges[i].End]) && (shortestPathTree[edges[i].End] == null))
					{
						fCosts[edges[i].End] = gCost + hCost;
						gCosts[edges[i].End] = gCost;

						queue.ChangePriority(edges[i].End);
						frontier[edges[i].End] = edges[i];
					}
				}
			}

			found = SearchState.NotCompleted;
			return found;
		}

		#endregion
	}
}
