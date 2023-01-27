#region Using Directives

using System;
using System.Collections.Generic;



using CasaEngine.AI.Graphs;
using CasaEngine.AI.Messaging;
using CasaEngine.AI.Navigation;
using Microsoft.Xna.Framework;

#endregion

namespace CasaEngine.AI.Pathfinding
{
	/// <summary>
	/// This class is capable of handling search requests done by a MovingEntity to move
	/// and navigate its environment
	/// </summary>
	/// <typeparam name="T">The type of the edges of the graph where the searchs will take place</typeparam>
	/// <remarks>
	/// The PathPlanner uses messages by default to comunicate with its owner entity about search results, so the
	/// owner should implement the <see cref="CasaEngine.AI.Messaging.IMessageable" /> interface
	/// </remarks>
	public class PathPlanner<T>
		where T : WeightedEdge
	{
		#region Constants

		/// <summary>
		/// No neighbour node was found
		/// </summary>
		public const int NoNodeFound = -1;

		#endregion

		#region Fields

		/// <summary>
		/// The owner entity of the PathPlanner
		/// </summary>
		protected internal MovingEntity owner;

		/// <summary>
		/// The graph where the pathfinding searches are going to happen
		/// </summary>
		protected internal Graph<NavigationNode, T> graph;

		/// <summary>
		/// The maximum distance the PathPlanner will use to search a neighbour-Runable
		/// node to a given position
		/// </summary>
		protected internal float neighboursSearchRange;

		/// <summary>
		/// Search algorithm used by the planner
		/// </summary>
		protected internal GraphSearchAlgorithm<NavigationNode, T> search;

		/// <summary>
		/// Destination point of the search
		/// </summary>
		protected internal Vector3 destination;

		/// <summary>
		/// Smooth algorithm for the path
		/// </summary>
		protected internal PathSmoother smoothAlgorithm;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="owner">The owner entity of the PathManager</param>
		/// <param name="graph">The graph where the pathfinding searches are going to happen</param>
		/// <param name="search">Search algorithm used by the planner</param>
		/// <param name="neighboursSearchRange">
		/// The maximum distance the PathPlanner will use to search a neighbour-Runable
		/// node to a given position
		/// </param>
		/// <param name="smoothAlgorithm">Smooth algorithm for the path</param>
		public PathPlanner(MovingEntity owner, Graph<NavigationNode, T> graph, GraphSearchAlgorithm<NavigationNode, T> search, float neighboursSearchRange, PathSmoother smoothAlgorithm)
		{
			this.owner = owner;
			this.graph = graph;
			this.search = search;
			this.neighboursSearchRange = neighboursSearchRange;
			this.smoothAlgorithm = smoothAlgorithm;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the owner of the planner
		/// </summary>
		public MovingEntity Owner
		{
			get { return owner; }
		}

		/// <summary>
		/// Gets or sets the graph where the searchs will happen
		/// </summary>
		public Graph<NavigationNode, T> Graph
		{
			get { return graph; }
			set { graph = value; }
		}

		/// <summary>
		/// Gets or sets the search algorithm used by the planner
		/// </summary>
		public GraphSearchAlgorithm<NavigationNode, T> Search
		{
			get { return search; }
			set { search = value; }
		}

		/// <summary>
		/// Gets or sets the maximum search distance for neighbours
		/// </summary>
		public float NeighboursSearchRange
		{
			get { return neighboursSearchRange; }
			set { neighboursSearchRange = value; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// This method requests a time-sliced search to a position
		/// </summary>
		/// <param name="position">The destination of the search</param>
		/// <returns>
		/// True if the search can take place (even if later it fails). False if it can´t take place: the start or end positions are
		/// unreachable for the owner entity (they are out of the geometry or another kind of problem)
		/// </returns>
		/// <remarks>A* is used by default as search algorithm</remarks>
		public bool RequestPathToPosition(Vector3 position)
		{
			int closestNodeToEntity, closestNodeToDestination;

			PathManager<T>.Instance.Unregister(this);

			destination = position;

			//If the entity can reach the destination directly, there´s no need to request a search
			if (owner.CanMoveBetween(owner.Position, destination) == true)
			{
                MessageManagerRouter.Instance.SendMessage(0, owner.ID, 0, (int)MessageType.PathReady, null);
				return true;
			}

			//Get the closest node to the entity
			closestNodeToEntity = ClosestNodeToPosition(owner.Position);
			if (closestNodeToEntity == NoNodeFound)
				return false;

			//Get the cloest node to the destination
			closestNodeToDestination = ClosestNodeToPosition(destination);
			if (closestNodeToDestination == NoNodeFound)
				return false;

			//Initialize the search
			search.Initialize(closestNodeToEntity, closestNodeToDestination);

			//Register the search in the PathManager
			PathManager<T>.Instance.Register(this);

			return true;
		}

		/// <summary>
		/// Gets a list of 3D positions from the location of the entity to the destination
		/// </summary>
		public List<Vector3> PathOfPositions
		{
			get
			{
				List<Vector3> path;

				path = NodesToPositions(search.PathOfNodes);
				path.Add(destination);

				return path;
			}
		}

		/// <summary>
		/// Gets a list of NavigationEdges from the location of the entity to the destination
		/// </summary>
		/// <remarks>The path is smoothed according to the choosen path algorithm</remarks>
		public List<NavigationEdge> PathOfEdges
		{
			get
			{
				List<int> pathNodes;
				List<NavigationEdge> pathEdges;

				//Get the path of nodes and transform it to a path of edges
				pathNodes = search.PathOfNodes;
				pathEdges = NodesToPathEdges(pathNodes);

				//If there´s at least one node in the path
				if (pathNodes.Count != 0)
				{
					//Add the edge between the position of the owner entity and the first node of the path
					pathEdges.Insert(0, new NavigationEdge(owner.Position, graph.GetNode(pathNodes[0]).Position, EdgeInformation.Normal));

					//Add the edge between the last node of the path and the destination
					pathEdges.Add(new NavigationEdge(graph.GetNode(pathNodes[pathNodes.Count - 1]).Position, destination, EdgeInformation.Normal));

					//Smooth the path
					smoothAlgorithm(owner, pathEdges);
				}

				else //The path is a straight line
					pathEdges.Add(new NavigationEdge(owner.Position, destination, EdgeInformation.Normal));

				return pathEdges;
			}
		}

		/// <summary>
		/// This method performs a full search for a path to reach the target position and returns
		/// the path of positions to it if it exists
		/// </summary>
		/// <param name="position">The destination of the search</param>
		/// <returns>The list of positions if the search was succesful, null if it wasn´t</returns>
		public List<Vector3> GetNowPathOfPositionsToPosition(Vector3 position)
		{
			int closestNodeToEntity, closestNodeToDestination;

			destination = position;

			//If the entity can reach the destination directly, there´s no need to request a search
			if (owner.CanMoveBetween(owner.Position, destination) == true)
				return PathOfPositions;

			//Get the closest node to the entity
			closestNodeToEntity = ClosestNodeToPosition(owner.Position);
			if (closestNodeToEntity == NoNodeFound)
				return null;

			//Get the cloest node to the destination
			closestNodeToDestination = ClosestNodeToPosition(destination);
			if (closestNodeToDestination == NoNodeFound)
				return null;

			//Do the full search and if succesful return the path of positions
			search.Initialize(closestNodeToEntity, closestNodeToEntity);
			if (search.Search() == SearchState.CompletedAndFound)
				return PathOfPositions;

			return null;
		}

		/// <summary>
		/// This method performs a full search for a path to reach the target position and returns
		/// the path of NavigationEdges to it if it exists
		/// </summary>
		/// <param name="position">The destination of the search</param>
		/// <returns>The list of NavigationEdges if the search was succesful, null if it wasn´t</returns>
		public List<NavigationEdge> GetNowPathOfEdgesToPosition(Vector3 position)
		{
			int closestNodeToEntity, closestNodeToDestination;

			destination = position;

			//If the entity can reach the destination directly, there´s no need to request a search
			if (owner.CanMoveBetween(owner.Position, destination) == true)
				return PathOfEdges;

			//Get the closest node to the entity
			closestNodeToEntity = ClosestNodeToPosition(owner.Position);
			if (closestNodeToEntity == NoNodeFound)
				return null;

			//Get the cloest node to the destination
			closestNodeToDestination = ClosestNodeToPosition(destination);
			if (closestNodeToDestination == NoNodeFound)
				return null;

			//Do the full search and if succesful return the path of positions
			search.Initialize(closestNodeToEntity, closestNodeToEntity);
			if (search.Search() == SearchState.CompletedAndFound)
				return PathOfEdges;

			return null;
		}

		/// <summary>
		/// This method makes one search cycle and messages the owner entity if the search was finished
		/// </summary>
		/// <returns>The state of the search</returns>
		protected internal SearchState CycleOnce()
		{
			SearchState result;

			result = search.CycleOnce();
			
			//If the search failed inform the owner
			if (result == SearchState.CompletedAndNotFound)
                MessageManagerRouter.Instance.SendMessage(0, owner.ID, 0, (int)MessageType.PathNotAvailable, null);

			//If the search succeeded inform the owner
			if (result == SearchState.CompletedAndFound)
                MessageManagerRouter.Instance.SendMessage(0, owner.ID, 0, (int)MessageType.PathReady, null);

			return result;
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// This method gets the closest node to a given position that the owner entity can reach
		/// </summary>
		/// <param name="position">The position we are going to test</param>
		/// <returns>The index in the graph of the closest node to the tested position</returns>
		private int ClosestNodeToPosition(Vector3 position)
		{
			int closestNode;
			float distance, closestDistance;
			List<NavigationNode> neighbours;

			//Ask the graph for the neighbour nodes to the position given a search distance
			neighbours = graph.GetNeighbourNodes(0, position, neighboursSearchRange);

			closestNode = NoNodeFound;
			closestDistance = float.MaxValue;

			//See the closest node the entity can reach
			for (int i = 0; i < neighbours.Count; i++)
			{
				//If the entity can reach the position update the closest node values
				if (owner.CanMoveBetween(position, neighbours[i].Position) == true)
				{
					distance = (position - neighbours[i].Position).LengthSquared();

					if (distance < closestDistance)
					{
						closestDistance = distance;
						closestNode = i;
					}
				}
			}

			return closestNode;
		}

		/// <summary>
		/// This method transform a list of graph nodes indexes into a list of 3D positions
		/// </summary>
		/// <param name="nodes">The list of nodes to transform</param>
		/// <returns>A list of 3D positions</returns>
		private List<Vector3> NodesToPositions(List<int> nodes)
		{
			List<Vector3> path;

			path = new List<Vector3>();

			//Get the position of each node
			for (int i = 0; i < nodes.Count; i++)
				path.Add(graph.GetNode(i).Position);

			return path;
		}

		/// <summary>
		/// This method transform a list of graph node indexes into a list of <see cref="NavigationEdge"/>
		/// </summary>
		/// <param name="nodes"></param>
		/// <returns></returns>
		private List<NavigationEdge> NodesToPathEdges(List<int> nodes)
		{
			List<NavigationEdge> path;
			Graph<NavigationNode, AnnotatedEdge> annotatedGraph;

			path = new List<NavigationEdge>();
			
			//Test if the graph is an annotated graph or not
			annotatedGraph = graph as Graph<NavigationNode, AnnotatedEdge>;
			if (annotatedGraph == null)
			{
				//Create the list without extra edge information
				for (int i = 0; i < nodes.Count - 1; i++)
					path.Add(new NavigationEdge(graph.GetNode(nodes[i]).Position, graph.GetNode(nodes[i + 1]).Position, EdgeInformation.Normal));
			}

			else
			{
				//Create the list with extra edge information
				for (int i = 0; i < nodes.Count - 1; i++)
					path.Add(new NavigationEdge(annotatedGraph.GetNode(nodes[i]).Position, annotatedGraph.GetNode(nodes[i + 1]).Position, annotatedGraph.GetEdge(nodes[i], nodes[i + 1]).Information));
			}

			return path;
		}

		#endregion
	}
}
