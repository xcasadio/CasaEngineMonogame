using CasaEngine.AI.Graphs;


namespace CasaEngine.AI.Pathfinding
{
    public class DepthFirstSearch<T, TK> : GraphSearchAlgorithm<T, TK>
        where T : Node
        where TK : Edge
    {

        protected internal List<Visibility> Visited;

        protected internal Stack<TK> StackedEdges;

        protected internal List<int> Route;



        public DepthFirstSearch(Graph<T, TK> graph)
            : base(graph)
        { }



        public override List<int> PathOfNodes
        {
            get
            {
                List<int> path;
                int node;

                //If the search didn´t find anything, return null
                if (Found != SearchState.CompletedAndFound)
                    return null;

                //Get the list of nodes from source to target. The route list is visited in reverse order
                path = new List<int>();
                node = Target;

                while (node != Source)
                {
                    path.Insert(0, node);
                    node = Route[node];
                }

                return path;
            }
        }

        public override List<TK> PathOfEdges
        {
            get
            {
                List<TK> path;
                List<int> nodes;

                //If the search didn´t find anything, return null
                if (Found != SearchState.CompletedAndFound)
                    return null;

                //Get the nodes that form the path
                nodes = PathOfNodes;

                //Get the edges between the nodes from the graph
                path = new List<TK>();
                for (int i = 0; i < nodes.Count - 1; i++)
                    path.Add(Graph.GetEdge(nodes[i], nodes[i + 1]));

                return path;
            }
        }



        public override void Initialize(int source, int target)
        {
            TK dummy;

            base.Initialize(source, target);

            Visited = new List<Visibility>();
            Route = new List<int>();

            for (int i = 0; i < Graph.NodeCount; i++)
            {
                Visited.Add(Visibility.Unvisited);
                Route.Add(Node.NoParent);
            }

            //Initialize the edges stack
            StackedEdges = new Stack<TK>();

            //Create a dummy edge to start the search
            dummy = Activator.CreateInstance<TK>();
            dummy.Start = source;
            dummy.End = source;
            StackedEdges.Push(dummy);
        }

        public override SearchState Search()
        {
            TK next;
            List<TK> nodeEdges;

            //While there are stacked edges
            while (StackedEdges.Count != 0)
            {
                next = StackedEdges.Pop();

                Route[next.End] = next.Start;
                Visited[next.End] = Visibility.Visited;

                //If the edge ends in the target, return success
                if (next.End == Target)
                {
                    Found = SearchState.CompletedAndFound;
                    return Found;
                }

                //Stack all child edges from this node that aren´t visited
                nodeEdges = Graph.GetEdgesFromNode(next.End);
                for (int i = 0; i < nodeEdges.Count; i++)
                    if (Visited[nodeEdges[i].End] == Visibility.Unvisited)
                        StackedEdges.Push(nodeEdges[i]);
            }

            //The search failed
            Found = SearchState.CompletedAndNotFound;
            return Found;
        }

        public override SearchState CycleOnce()
        {
            TK next;
            List<TK> nodeEdges;

            //If the stack is empty, the search was a failure
            if (StackedEdges.Count == 0)
            {
                Found = SearchState.CompletedAndNotFound;
                return Found;
            }

            //Pop an edge
            next = StackedEdges.Pop();

            Route[next.End] = next.Start;
            Visited[next.End] = Visibility.Visited;

            //If the edge ends in the target, return success
            if (next.End == Target)
            {
                Found = SearchState.CompletedAndFound;
                return Found;
            }

            //Stack all child edges from this node that aren´t visited
            nodeEdges = Graph.GetEdgesFromNode(next.End);
            for (int i = 0; i < nodeEdges.Count; i++)
                if (Visited[nodeEdges[i].End] == Visibility.Unvisited)
                    StackedEdges.Push(nodeEdges[i]);

            Found = SearchState.NotCompleted;
            return Found;
        }

    }
}
