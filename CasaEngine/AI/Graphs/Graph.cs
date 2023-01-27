#region Using Directives

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Graphs
{
	/// <summary>
	/// This class represents a graph
	/// </summary>
	/// <remarks>
	/// This implementation uses a list of edges to determine connectivity between two
	/// nodes. This kind of implementation is better suited for sparse graphs. Based
	/// on Mat Buckland implementation from his book "Programming Game AI by Example"
	/// </remarks>
	/// <typeparam name="T">The type of the nodes</typeparam>
	/// <typeparam name="K">The type of the edges</typeparam>
	[Serializable]
	public class Graph<T, K>
		where T : Node
		where K : Edge
	{
		#region Fields

		/// <summary>
		/// Next node index when adding a new node to the graph
		/// </summary>
		protected internal int nextNode;

		/// <summary>
		/// The list of nodes of the graph
		/// </summary>
		protected internal List<T> nodes;

		/// <summary>
		/// The list of edges that come out from each node
		/// </summary>
		protected internal List<List<K>> edges;

		/// <summary>
		/// Indicates if the graph is a digraph (edges are directional: the same two nodes can be 
		/// connected by two edges, one on each direction)
		/// </summary>
		protected internal bool digraph;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="digraph">Indicates if the graph is a digraph or not</param>
		public Graph(bool digraph)
		{
			this.digraph = digraph;

			nodes = new List<T>();
			edges = new List<List<K>>();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the total number of nodes in the graph
		/// </summary>
		public int NodeCount
		{
			get { return nodes.Count; }
		}

		/// <summary>
		/// Gets the number of active nodes in the graph
		/// </summary>
		public int ActiveNodeCount
		{
			get
			{
				int count = 0;

				for (int i = 0; i < nodes.Count; i++)
					if (nodes[i].Index != Edge.InvalidNode)
						count++;

				return count;
			}
		}

		/// <summary>
		/// Gets the total number of edges in the graph
		/// </summary>
		public int EdgesCount
		{
			get
			{
				int count = 0;

				for (int i = 0; i < edges.Count; i++)
					count += edges[i].Count;

				return count;
			}
		}

		/// <summary>
		/// Gets the number of active edges in the graph
		/// </summary>
		public int ActiveEdgesCount
		{
			get
			{
				int count = 0;

				for (int i = 0; i < edges.Count; i++)
					for (int j = 0; j < edges[i].Count; j++)
						if ((nodes[edges[i][j].Start].Index != Edge.InvalidNode) && (nodes[edges[i][j].End].Index != Edge.InvalidNode))
							count++;

				return count;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Indicates if a node is active or not
		/// </summary>
		/// <param name="index">Node to test</param>
		/// <returns>True if the node is active, false if it is not</returns>
		public bool IsNodeActive(int index)
		{
			if (index < 0 || index >= nodes.Count)
				return false;

			if (nodes[index].Index == Edge.InvalidNode)
				return false;

			return true;
		}

		/// <summary>
		/// Tests if a edge exists between two nodes
		/// </summary>
		/// <param name="start">Start node index</param>
		/// <param name="end">End node index</param>
		/// <returns>True if the edge exists, false if it doesn´t</returns>
		public bool IsEdgePresent(int start, int end)
		{
			if (IsNodeActive(start) && IsNodeActive(end))
				for (int i = 0; i < edges[start].Count; i++)
					if (edges[start][i].End == end)
						return true;

			return false;
		}

		/// <summary>
		/// Gets a node
		/// </summary>
		/// <param name="index">Node to get</param>
		/// <returns>The node</returns>
		public T GetNode(int index)
		{
			if (index < 0 || index > nodes.Count)
				throw new Exception("Invalid index");
			
			return nodes[index];
		}

		/// <summary>
		/// Gets an edge
		/// </summary>
		/// <param name="start">Start node of the edge</param>
		/// <param name="end">End node of the edge</param>
		/// <returns>The edge</returns>
		public K GetEdge(int start, int end)
		{
			if (IsNodeActive(start) && IsNodeActive(end))
				for (int i = 0; i < edges[start].Count; i++)
					if (edges[start][i].End == end)
						return edges[start][i];

			throw new Exception("Invalid edge");
		}

		/// <summary>
		/// Gets all the edges from a node
		/// </summary>
		/// <param name="nodeIndex">The node index</param>
		/// <returns>The list of edges of the node</returns>
		public List<K> GetEdgesFromNode(int nodeIndex)
		{
			if (nodeIndex < 0 || nodeIndex > nodes.Count)
				throw new Exception("Invalid index");

			return edges[nodeIndex];
		}

		/// <summary>
		/// Adds a new edge to the graph
		/// </summary>
		/// <param name="edge"></param>
		public void AddEdge(K edge)
		{
			K reverseEdge;

			if (IsNodeActive(edge.Start) && IsNodeActive(edge.End))
				if (IsEdgePresent(edge.Start, edge.End) == false)
					edges[edge.Start].Add(edge);

			//If the graph is a digraph, add the reversed edge
			if (digraph)
				if (IsEdgePresent(edge.End, edge.Start) == false)
				{
					reverseEdge = (K) edge.Clone();
					reverseEdge.Start = edge.End;
					reverseEdge.End = edge.Start;

					edges[edge.End].Add(reverseEdge);
				}
		}

		/// <summary>
		/// Deletes and edge from the graph
		/// </summary>
		/// <param name="start">Start node of the edge</param>
		/// <param name="end">End node of the edge</param>
		public void RemoveEdge(int start, int end)
		{
			if (IsNodeActive(start) && IsNodeActive(end))
			{
				for (int i = 0; i < edges[start].Count; i++)
					if (edges[start][i].End == end)
					{
						edges[start].Remove(edges[start][i]);
						break;
					}

				//If the graph is a digraph remove the revesed edge
				if (digraph)
					for (int i = 0; i < edges[end].Count; i++)
						if (edges[end][i].End == start)
						{
							edges[end].Remove(edges[end][i]);
							break;
						}

			}
		}

		/// <summary>
		/// Adds a node to the graph
		/// </summary>
		/// <remarks>
		/// The node can´t have an index value assigned to it
		/// </remarks>
		/// <param name="node"></param>
		public void AddNode(T node)
		{
			if (node.Index == Edge.InvalidNode)
			{
				node.Index = nextNode++;
				nodes.Add(node);
				edges.Add(new List<K>());
			}

			else
				throw new Exception("A node can´t have an index before being added to a graph");
        }

		/// <summary>
		/// Deletes a node from the graph
		/// </summary>
		/// <remarks>
		/// The node can´t have an index value assigned to it
		/// </remarks>
		/// <param name="node"></param>
		public void RemoveNode(T node)
		{
			int nodeIndex = nodes.IndexOf(node);
			List<K> toDelete = new List<K>();

			edges.RemoveAt(nodeIndex);
			nodes.Remove(node);

			foreach (List<K> edgeList in edges)
			{
				toDelete.Clear();

				foreach (K edge in edgeList)
				{
					if (edge.Start == nodeIndex || edge.End == nodeIndex)
					{
						toDelete.Add(edge);
					}
				}

				foreach (K edge in toDelete)
				{
					edgeList.Remove(edge);
				}
			}
		}

		/// <summary>
		/// Gets the neighbour nodes of a point in space
		/// </summary>
		/// <param name="spacePartitionSector">The sector in the space partition structure</param>
		/// <param name="searchPosition">The point used to search</param>
		/// <param name="searchRange">The maximum distance we want to search from the start position.
		/// This distance must not be a squared distance</param>
		/// <returns>The list of neighboring nodes to the point</returns>
		/// <remarks>The knowledge of neighbourhood is in the nodes not in the graph</remarks>
		public List<T> GetNeighbourNodes(int spacePartitionSector, Vector3 searchPosition, float searchRange)
		{
			List<T> neighbours;

			neighbours = new List<T>();
			for (int i = 0; i < nodes.Count; i++)
				if (nodes[i].IsNeighbour(spacePartitionSector, searchPosition, searchRange) == true)
					neighbours.Add(nodes[i]);

			return neighbours;
		}

		#endregion
	}
}
