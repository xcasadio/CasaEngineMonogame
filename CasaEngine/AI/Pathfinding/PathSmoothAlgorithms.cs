#region Using Directives

using System;
using System.Collections.Generic;

using CasaEngine.AI.Graphs;
using CasaEngine.AI.Navigation;

#endregion

namespace CasaEngine.AI.Pathfinding
{
	/// <summary>
	/// This class has several path smooth algorithms implementations
	/// </summary>
	public static class PathSmoothAlgorithms
	{
		/// <summary>
		/// This smothing algorithm doesn´t modify the path in any way
		/// </summary>
		/// <param name="entity">The entity that´s going to move through the path</param>
		/// <param name="path">The path the algorithm is going to try to smooth</param>
		public static void NoSmooth(MovingEntity entity, List<NavigationEdge> path)
			{}

		/// <summary>
		/// This algorithm does a quick smooth of the path, enough to make it much more better looking
		/// and without using too much CPU
		/// </summary>
		/// <param name="entity">The entity that´s going to move through the path</param>
		/// <param name="path">The path the algorithm is going to try to smooth</param>
		/// <remarks>
		/// The check done for the edge information can fail sometimes, it depends on the map type.
		/// In complex maps a more carefully check should be done to see if the entity can reach the next edge
		/// end-point without changing its movement behavior (traversing the same terrain type)
		/// </remarks>
		public static void PathSmoothQuick(MovingEntity entity, List<NavigationEdge> path)
		{
			int i, j;

			i = 0;
			j = 1;
			
			//Move through all the edges of the path testing if the entity can move through 2 non-consecutive points
			//and the edge information is the same in the edges to smooth
			while (j  < path.Count)
			{
				if (entity.CanMoveBetween(path[i].Start, path[j].End) && path[i].Information == path[j].Information)
				{
					path[i].End = path[j].End;
					path.RemoveAt(j);
				}

				else
				{
					i = j;
					j++;
				}
			}
		}

		/// <summary>
		/// This algorithm does a precise smooth of the path, making it as good as it can get,
		/// but using CPU more intesively
		/// </summary>
		/// <param name="entity">The entity that´s going to move through the path</param>
		/// <param name="path">The path the algorithm is going to try to smooth</param>
		/// <remarks>
		/// The check done for the edge information can fail sometimes, it depends on the map type.
		/// In complex maps a more carefully check should be done to see if the entity can reach the next edge
		/// end-point without changing its movement behavior (traversing the same terrain type)
		/// </remarks>
		public static void PathSmoothPrecise(MovingEntity entity, List<NavigationEdge> path)
		{
			int i, j;

			i = 0;

			//This method does the smoothing check between all pairs of nodes trying to see if edges can be removed from the path
			while (i < path.Count)
			{
				j = i + 1;

				while (j < path.Count)
				{
					if (entity.CanMoveBetween(path[i].Start, path[j].End) && path[i].Information == path[j].Information)
					{
						path[i].End = path[j].End;
						path.RemoveRange(i, j - i);
						i = j;
						i--;
					}

					else
						j++;
				}

				i++;
			}
		}
	}
}
