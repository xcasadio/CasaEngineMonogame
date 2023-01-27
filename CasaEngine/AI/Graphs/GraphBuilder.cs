#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;

#endregion

namespace CasaEngine.AI.Graphs
{
	/// <summary>
	/// Helper class for graphs
	/// </summary>
	/// <remarks>
	/// This class has only barebone functionality at the moment
	/// </remarks>
	public static class GraphBuilder
	{
		#region Methods

		/// <summary>
		/// Reads a graph from a matrix file. The format for the graph must be similar to a tile based graph
		/// </summary>
		/// <example>
		/// File format example:
		/// 3
		/// 3
		/// 1 2 3
		/// 4 5 6
		/// 7 8 9
		/// The first number is the height of the graph, the next one the width, and then the
		/// node weights for all nodes of the graph.
		/// </example>
		/// <remarks>
		/// -1 represents the infinite cost. Quite simple, but useful to load sample nav graphs
		/// </remarks>
		/// <typeparam name="T">The type of the nodes</typeparam>
		/// <typeparam name="K">The type of the edges</typeparam>
		/// <param name="filename">File that contains the graph</param>
		/// <returns>The graph from the file</returns>
		public static Graph<T, K> NavGraphFromMatrixFile<T, K>(string filename)
			where T : NavigationNode
			where K : WeightedEdge
		{
			StreamReader file;
			int width, height, weight, xPos, yPos;
			String[] elements, separators;
			int[,] weights;
			Graph<T, K> graph;
			List<K> edges;
			T node;
			K edge;

			using (file = new StreamReader(filename))
			{
				//Get the dimensions of the graph
				if (int.TryParse(file.ReadLine(), out height) == false)
					throw new Exception("Height expected");

				if (int.TryParse(file.ReadLine(), out width) == false)
					throw new Exception("Width expected");

				weights = new int[height, width];

				//Get the weights
				separators = new string[1];
				separators[0] = " ";
				for (int i = 0; i < height; i++)
				{
					elements = file.ReadLine().Split(separators, StringSplitOptions.RemoveEmptyEntries);
					
					if (elements.Length != width)
						throw new Exception("Incorrect number of elements in row " + (i + 1));

					for (int j = 0; j < elements.Length; j++)
					{
						if (int.TryParse(elements[j], out weight) == false)
							throw new Exception("Weight expected");

						weights[i,j] = weight;
					}
				}

				//Generate all the nodes
				graph = new Graph<T, K>(true);
				for (int i = 0; i < height; i++)
					for (int j = 0; j < width; j++)
					{
						node = Activator.CreateInstance<T>();
						node.Position = new Vector3(j, i, 0);
						graph.AddNode(node);
					}

				//Generate the edges
				for (int z = 0; z < graph.ActiveNodeCount; z++)
				{
					edges = graph.GetEdgesFromNode(z);
					xPos = graph.GetNode(z).Index % width;
					yPos = graph.GetNode(z).Index / width;

					//For each node, generate the edges around it
					for (int i = -1; i < 2; i++)
					{
						for (int j = -1; j < 2; j++)
						{
							if (i == 0 && j == 0)
								continue;

							//The position where the edge is going to point must be a valid position
							if (ValidatePosition(width, height, xPos + j, yPos + i))
							{
								edge = Activator.CreateInstance<K>();
								
								//Set the edge values
								edge.Start = z;
								edge.End = xPos + j + width * (yPos + i);

								if (weights[yPos + i, xPos + j] == -1)
									edge.Cost = float.MaxValue;

								else
									edge.Cost = weights[yPos + i, xPos + j];

								edges.Add(edge);
							}
						}
					}
				}
			}

			return graph;
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Validates if a position is in the graph or outside it
		/// </summary>
		/// <param name="width">Width of the graph</param>
		/// <param name="height">Height of the graph</param>
		/// <param name="x">X coordinate of the position</param>
		/// <param name="y">Y coordiante of the position</param>
		/// <returns>True if (x, y) is a valid position, false if it is not</returns>
		private static bool ValidatePosition(int width, int height, int x, int y)
		{
			if (x < 0 || x >= width)
				return false;

			if (y < 0 || y >= height)
				return false;

			return true;
		}

		#endregion
	}
}
