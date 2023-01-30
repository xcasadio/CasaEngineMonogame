using Microsoft.Xna.Framework;




namespace CasaEngine.AI.Graphs
{
    public static class Heuristics<T, K>
        where T : NavigationNode
        where K : WeightedEdge
    {
        public static double EuclideanDistance(Graph<T, K> graph, int node1, int node2)
        {
            return (graph.GetNode(node1).Position - graph.GetNode(node2).Position).LengthSquared();
        }

        public static double ManhattanDistance(Graph<T, K> graph, int node1, int node2)
        {
            Vector3 aux;

            aux = graph.GetNode(node1).Position - graph.GetNode(node2).Position;
            return System.Math.Abs(aux.X) + System.Math.Abs(aux.Y) + System.Math.Abs(aux.Z);
        }

        public static double DjisktraDistance(Graph<T, K> graph, int node1, int node2)
        {
            return 0;
        }
    }
}
