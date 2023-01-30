using CasaEngine.AI.Navigation;


namespace CasaEngine.AI.Pathfinding
{
    public static class PathSmoothAlgorithms
    {
        public static void NoSmooth(MovingEntity entity, List<NavigationEdge> path)
        { }

        public static void PathSmoothQuick(MovingEntity entity, List<NavigationEdge> path)
        {
            int i, j;

            i = 0;
            j = 1;

            //Move through all the edges of the path testing if the entity can move through 2 non-consecutive points
            //and the edge information is the same in the edges to smooth
            while (j < path.Count)
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
