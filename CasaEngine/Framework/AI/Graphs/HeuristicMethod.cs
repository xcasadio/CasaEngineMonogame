namespace CasaEngine.Framework.AI.Graphs
{
    public delegate double HeuristicMethod<T, TK>(Graph<T, TK> graph, int node1, int node2) where T : NavigationNode where TK : WeightedEdge;
}
