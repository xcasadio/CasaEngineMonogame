namespace CasaEngine.AI.Graphs
{
    public delegate double HeuristicMethod<T, K>(Graph<T, K> graph, int node1, int node2) where T : NavigationNode where K : WeightedEdge;
}
