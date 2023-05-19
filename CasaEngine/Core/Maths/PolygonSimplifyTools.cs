//Copied from VelcroPhysics : Genbox.VelcroPhysics.Tools.PolygonManipulation.SimplifyTools

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using System.Diagnostics.Metrics;
using CasaEngine.Core.Helpers;

namespace CasaEngine.Core.Maths
{
    public static class PolygonSimplifyTools
    {
        /// <summary>Removes all collinear points on the polygon.</summary>
        /// <param name="vertices">The polygon that needs simplification.</param>
        /// <param name="collinearityTolerance">The collinearity tolerance.</param>
        /// <returns>A simplified polygon.</returns>
        public static List<Vector2> CollinearSimplify(List<Vector2> vertices, float collinearityTolerance = 0)
        {
            if (vertices.Count <= 3)
                return vertices;

            List<Vector2> simplified = new List<Vector2>(vertices.Count);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 prev = vertices[i - 1 < 0 ? vertices.Count - 1 : i - 1];
                Vector2 current = vertices[i];
                Vector2 next = vertices[i + 1 > vertices.Count ? 0 : i + 1];

                //If they collinear, continue
                if (Vector2Helper.IsCollinear(ref prev, ref current, ref next, collinearityTolerance))
                    continue;

                simplified.Add(current);
            }

            return simplified;
        }

        /// <summary>
        /// Ramer-Douglas-Peucker polygon simplification algorithm. This is the general recursive version that does not
        /// use the speed-up technique by using the Melkman convex hull. If you pass in 0, it will remove all collinear points.
        /// </summary>
        /// <returns>The simplified polygon</returns>
        public static List<Vector2> DouglasPeuckerSimplify(List<Vector2> vertices, float distanceTolerance)
        {
            if (vertices.Count <= 3)
                return vertices;

            bool[] usePoint = new bool[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                usePoint[i] = true;
            }

            SimplifySection(vertices, 0, vertices.Count - 1, usePoint, distanceTolerance);

            List<Vector2> simplified = new List<Vector2>(vertices.Count);

            for (int i = 0; i < vertices.Count; i++)
            {
                if (usePoint[i])
                    simplified.Add(vertices[i]);
            }

            return simplified;
        }

        private static void SimplifySection(List<Vector2> vertices, int i, int j, bool[] usePoint, float distanceTolerance)
        {
            if (i + 1 == j)
                return;

            Vector2 a = vertices[i];
            Vector2 b = vertices[j];

            double maxDistance = -1.0;
            int maxIndex = i;
            for (int k = i + 1; k < j; k++)
            {
                Vector2 point = vertices[k];

                double distance = LineHelper.DistanceBetweenPointAndLineSegment(ref point, ref a, ref b);

                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    maxIndex = k;
                }
            }

            if (maxDistance <= distanceTolerance)
            {
                for (int k = i + 1; k < j; k++)
                {
                    usePoint[k] = false;
                }
            }
            else
            {
                SimplifySection(vertices, i, maxIndex, usePoint, distanceTolerance);
                SimplifySection(vertices, maxIndex, j, usePoint, distanceTolerance);
            }
        }

        /// <summary>Merges all parallel edges in the list of vertices</summary>
        /// <param name="vertices">The vertices.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static List<Vector2> MergeParallelEdges(List<Vector2> vertices, float tolerance)
        {
            //From Eric Jordan's convex decomposition library

            if (vertices.Count <= 3)
                return vertices; //Can't do anything useful here to a triangle

            bool[] mergeMe = new bool[vertices.Count];
            int count = vertices.Count;

            //Gather points to process
            for (int i = 0; i < vertices.Count; ++i)
            {
                int lower = i == 0 ? vertices.Count - 1 : i - 1;
                int middle = i;
                int upper = i == vertices.Count - 1 ? 0 : i + 1;

                float dx0 = vertices[middle].X - vertices[lower].X;
                float dy0 = vertices[middle].Y - vertices[lower].Y;
                float dx1 = vertices[upper].X - vertices[middle].X;
                float dy1 = vertices[upper].Y - vertices[middle].Y;
                float norm0 = (float)Math.Sqrt(dx0 * dx0 + dy0 * dy0);
                float norm1 = (float)Math.Sqrt(dx1 * dx1 + dy1 * dy1);

                if (!(norm0 > 0.0f && norm1 > 0.0f) && count > 3)
                {
                    //Merge identical points
                    mergeMe[i] = true;
                    --count;
                }

                dx0 /= norm0;
                dy0 /= norm0;
                dx1 /= norm1;
                dy1 /= norm1;
                float cross = dx0 * dy1 - dx1 * dy0;
                float dot = dx0 * dx1 + dy0 * dy1;

                if (Math.Abs(cross) < tolerance && dot > 0 && count > 3)
                {
                    mergeMe[i] = true;
                    --count;
                }
                else
                    mergeMe[i] = false;
            }

            if (count == vertices.Count || count == 0)
                return vertices;

            int currIndex = 0;

            //Copy the vertices to a new list and clear the old
            List<Vector2> newVertives = new List<Vector2>(count);

            for (int i = 0; i < vertices.Count; ++i)
            {
                if (mergeMe[i] || currIndex == count)
                    continue;

                Debug.Assert(currIndex < count);

                newVertives.Add(vertices[i]);
                ++currIndex;
            }

            return newVertives;
        }

        /// <summary>Merges the identical points in the polygon.</summary>
        /// <param name="vertices">The vertices.</param>
        public static List<Vector2> MergeIdenticalPoints(List<Vector2> vertices)
        {
            HashSet<Vector2> unique = new HashSet<Vector2>();

            foreach (Vector2 vertex in vertices)
            {
                unique.Add(vertex);
            }

            return new List<Vector2>(unique);
        }

        /// <summary>Reduces the polygon by distance.</summary>
        /// <param name="vertices">The vertices.</param>
        /// <param name="distance">The distance between points. Points closer than this will be removed.</param>
        public static List<Vector2> ReduceByDistance(List<Vector2> vertices, float distance)
        {
            if (vertices.Count <= 3)
                return vertices;

            float distance2 = distance * distance;

            List<Vector2> simplified = new List<Vector2>(vertices.Count);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 current = vertices[i];
                Vector2 next = vertices[i + 1 > vertices.Count ? 0 : i + 1];

                //If they are closer than the distance, continue
                if ((next - current).LengthSquared() <= distance2)
                    continue;

                simplified.Add(current);
            }

            return simplified;
        }

        /// <summary>Reduces the polygon by removing the Nth vertex in the vertices list.</summary>
        /// <param name="vertices">The vertices.</param>
        /// <param name="nth">The Nth point to remove. Example: 5.</param>
        /// <returns></returns>
        public static List<Vector2> ReduceByNth(List<Vector2> vertices, int nth)
        {
            if (vertices.Count <= 3)
                return vertices;

            if (nth == 0)
                return vertices;

            List<Vector2> simplified = new List<Vector2>(vertices.Count);

            for (int i = 0; i < vertices.Count; i++)
            {
                if (i % nth == 0)
                    continue;

                simplified.Add(vertices[i]);
            }

            return simplified;
        }

        /// <summary>
        /// Simplify the polygon by removing all points that in pairs of 3 have an area less than the tolerance. Pass in 0
        /// as tolerance, and it will only remove collinear points.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="areaTolerance"></param>
        /// <returns></returns>
        public static List<Vector2> ReduceByArea(List<Vector2> vertices, float areaTolerance)
        {
            //From physics2d.net

            if (vertices.Count <= 3)
                return vertices;

            if (areaTolerance < 0)
                throw new ArgumentOutOfRangeException(nameof(areaTolerance), "must be equal to or greater than zero.");

            List<Vector2> simplified = new List<Vector2>(vertices.Count);
            Vector2 v3;
            Vector2 v1 = vertices[^2];
            Vector2 v2 = vertices[^1];
            areaTolerance *= 2;

            for (int i = 0; i < vertices.Count; ++i, v2 = v3)
            {
                v3 = i == vertices.Count - 1 ? simplified[0] : vertices[i];

                Vector2Helper.Cross(ref v1, ref v2, out float old1);
                Vector2Helper.Cross(ref v2, ref v3, out float old2);
                Vector2Helper.Cross(ref v1, ref v3, out float new1);

                if (Math.Abs(new1 - (old1 + old2)) > areaTolerance)
                {
                    simplified.Add(v2);
                    v1 = v2;
                }
            }

            return simplified;
        }
    }
}
