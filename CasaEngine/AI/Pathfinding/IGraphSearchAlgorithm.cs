using CasaEngine.AI.Graphs;


namespace CasaEngine.AI.Pathfinding
{
    public interface IGraphSearchAlgorithm<T>
        where T : Edge
    {

        List<int> PathOfNodes
        {
            get;
        }

        List<T> PathOfEdges
        {
            get;
        }



        void Initialize(int source, int target);

        SearchState Search();

        SearchState CycleOnce();

    }
}
