using Microsoft.Xna.Framework;




namespace CasaEngine.AI.Graphs
{
    public static class Heuristics<T, TK>
        where T : NavigationNode
        where TK : WeightedEdge
    {
        public static double EuclideanDistance(Graph<T, TK> graph, int node1, int node2)
        {
            return (graph.GetNode(node1).Position - graph.GetNode(node2).Position).LengthSquared();
        }

        public static double ManhattanDistance(Graph<T, TK> graph, int node1, int node2)
        {
            Vector3 aux;

            aux = graph.GetNode(node1).Position - graph.GetNode(node2).Position;
            return Math.Abs(aux.X) + Math.Abs(aux.Y) + Math.Abs(aux.Z);
        }

        public static double DjisktraDistance(Graph<T, TK> graph, int node1, int node2)
        {
            return 0;
        }
    }
}
