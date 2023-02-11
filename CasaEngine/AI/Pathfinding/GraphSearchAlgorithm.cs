using CasaEngine.AI.Graphs;



namespace CasaEngine.AI.Pathfinding
{
    public abstract class GraphSearchAlgorithm<T, TK> : IGraphSearchAlgorithm<TK>
        where T : Node
        where TK : Edge
    {

        protected internal Graph<T, TK> Graph;

        protected internal int Source;

        protected internal int Target;

        protected internal SearchState Found;



        protected GraphSearchAlgorithm(Graph<T, TK> graph)
        {
            string message = string.Empty;

            if (ValidateGraph(graph, ref message) == false)
            {
                throw new AiException("graph", GetType().ToString(), message);
            }

            Graph = graph;
            Found = SearchState.NotCompleted;
        }



        public static bool ValidateGraph(Graph<T, TK> graph, ref string message)
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

        public abstract List<TK> PathOfEdges
        {
            get;
        }



        public virtual void Initialize(int source, int target)
        {
            string message = string.Empty;

            if (ValidateNode(source, ref message) == false)
            {
                throw new AiException("source", GetType().ToString(), message);
            }

            if (ValidateNode(target, ref message) == false)
            {
                throw new AiException("target", GetType().ToString(), message);
            }

            Source = source;
            Target = target;
            Found = SearchState.NotCompleted;
        }

        public abstract SearchState Search();

        public abstract SearchState CycleOnce();

    }
}
