using CasaEngine.AI.Graphs;



namespace CasaEngine.AI.Pathfinding
{
    public abstract class GraphSearchAlgorithm<T, K> : IGraphSearchAlgorithm<K>
        where T : Node
        where K : Edge
    {

        protected internal Graph<T, K> graph;

        protected internal int source;

        protected internal int target;

        protected internal SearchState found;



        protected GraphSearchAlgorithm(Graph<T, K> graph)
        {
            String message = String.Empty;

            if (ValidateGraph(graph, ref message) == false)
                throw new AIException("graph", this.GetType().ToString(), message);

            this.graph = graph;
            this.found = SearchState.NotCompleted;
        }



        public static bool ValidateGraph(Graph<T, K> graph, ref string message)
        {
            if (graph == null)
            {
                message = "The graph where the search will be performed can´t be null";
                return false;
            }

            return true;
        }

        public static bool ValidateNode(int index, ref string message)
        {
            if (index < 0)
            {
                message = "The node index can´t be lower than 0";
                return false;
            }

            return true;
        }



        public abstract List<int> PathOfNodes
        {
            get;
        }

        public abstract List<K> PathOfEdges
        {
            get;
        }



        public virtual void Initialize(int source, int target)
        {
            String message = String.Empty;

            if (ValidateNode(source, ref message) == false)
                throw new AIException("source", this.GetType().ToString(), message);

            if (ValidateNode(target, ref message) == false)
                throw new AIException("target", this.GetType().ToString(), message);

            this.source = source;
            this.target = target;
            this.found = SearchState.NotCompleted;
        }

        public abstract SearchState Search();

        public abstract SearchState CycleOnce();

    }
}
