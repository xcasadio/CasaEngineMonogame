using CasaEngine.Framework.AI.Graphs;


namespace CasaEngine.Framework.AI.Pathfinding
{
    public class WidthFirstSearch<T, TK> : GraphSearchAlgorithm<T, TK>
        where T : Node
        where TK : Edge
    {

        protected internal List<Visibility> Visited;

        protected internal Queue<TK> QueuedEdges;

        protected internal List<int> Route;



        public WidthFirstSearch(Graph<T, TK> graph) : base(graph)
        { }



        public override List<int> PathOfNodes
        {
            get
            {
                List<int> path;
                int node;

                //If the search didn´t find anything, return null
                if (Found != SearchState.CompletedAndFound)
                {
                    return null;
                }

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
                {
                    return null;
                }

                //Get the nodes that form the path
                nodes = PathOfNodes;

                //Get the edges between the nodes from the graph
                path = new List<TK>();
                for (var i = 0; i < nodes.Count - 1; i++)
                {
                    path.Add(Graph.GetEdge(nodes[i], nodes[i + 1]));
                }

                return path;
            }
        }



        public override void Initialize(int source, int target)
        {
            TK dummy;

            //Initialize the base
            base.Initialize(source, target);

            Visited = new List<Visibility>();
            Route = new List<int>();

            //Initialize list values
            for (var i = 0; i < Graph.NodeCount; i++)
            {
                Visited.Add(Visibility.Unvisited);
                Route.Add(Node.NoParent);
            }

            //Initialize the edges queue
            QueuedEdges = new Queue<TK>();

            //Create a dummy edge that will start the search
            dummy = Activator.CreateInstance<TK>();
            dummy.Start = source;
            dummy.End = source;
            QueuedEdges.Enqueue(dummy);
            Visited[source] = Visibility.Visited;
        }

        public override SearchState Search()
        {
            TK next;
            List<TK> nodeEdges;

            //While there are enqueued edges
            while (QueuedEdges.Count != 0)
            {
                next = QueuedEdges.Dequeue();

                Route[next.End] = next.Start;

                //If the edge ends in the target, return success
                if (next.End == Target)
                {
                    Found = SearchState.CompletedAndFound;
                    return Found;
                }

                //Enqueue all child edges from this node that aren´t visited
                nodeEdges = Graph.GetEdgesFromNode(next.End);
                for (var i = 0; i < nodeEdges.Count; i++)
                {
                    if (Visited[nodeEdges[i].End] == Visibility.Unvisited)
                    {
                        QueuedEdges.Enqueue(nodeEdges[i]);
                        Visited[next.End] = Visibility.Visited;
                    }
                }
            }

            //The search failed
            Found = SearchState.CompletedAndNotFound;
            return Found;
        }

        public override SearchState CycleOnce()
        {
            TK next;
            List<TK> nodeEdges;

            //If the queue is empty, the search was a failure
            if (QueuedEdges.Count == 0)
            {
                Found = SearchState.CompletedAndNotFound;
                return Found;
            }

            //Dequeue an edge
            next = QueuedEdges.Dequeue();

            Route[next.End] = next.Start;

            //If the edge ends in the target, return success
            if (next.End == Target)
            {
                Found = SearchState.CompletedAndFound;
                return Found;
            }

            //Enqueue all child edges from this node that aren´t visited
            nodeEdges = Graph.GetEdgesFromNode(next.End);
            for (var i = 0; i < nodeEdges.Count; i++)
            {
                if (Visited[nodeEdges[i].End] == Visibility.Unvisited)
                {
                    QueuedEdges.Enqueue(nodeEdges[i]);
                    Visited[next.End] = Visibility.Visited;
                }
            }

            Found = SearchState.NotCompleted;
            return Found;
        }

    }
}
