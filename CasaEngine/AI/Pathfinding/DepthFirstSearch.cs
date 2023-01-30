using CasaEngine.AI.Graphs;


namespace CasaEngine.AI.Pathfinding
{
    public class DepthFirstSearch<T, K> : GraphSearchAlgorithm<T, K>
        where T : Node
        where K : Edge
    {

        protected internal List<Visibility> visited;

        protected internal Stack<K> stackedEdges;

        protected internal List<int> route;



        public DepthFirstSearch(Graph<T, K> graph)
            : base(graph)
        { }



        public override List<int> PathOfNodes
        {
            get
            {
                List<int> path;
                int node;

                //If the search didn´t find anything, return null
                if (found != SearchState.CompletedAndFound)
                    return null;

                //Get the list of nodes from source to target. The route list is visited in reverse order
                path = new List<int>();
                node = target;

                while (node != source)
                {
                    path.Insert(0, node);
                    node = route[node];
                }

                return path;
            }
        }

        public override List<K> PathOfEdges
        {
            get
            {
                List<K> path;
                List<int> nodes;

                //If the search didn´t find anything, return null
                if (found != SearchState.CompletedAndFound)
                    return null;

                //Get the nodes that form the path
                nodes = PathOfNodes;

                //Get the edges between the nodes from the graph
                path = new List<K>();
                for (int i = 0; i < nodes.Count - 1; i++)
                    path.Add(graph.GetEdge(nodes[i], nodes[i + 1]));

                return path;
            }
        }



        public override void Initialize(int source, int target)
        {
            K dummy;

            base.Initialize(source, target);

            visited = new List<Visibility>();
            route = new List<int>();

            for (int i = 0; i < graph.NodeCount; i++)
            {
                visited.Add(Visibility.Unvisited);
                route.Add(Node.NoParent);
            }

            //Initialize the edges stack
            stackedEdges = new Stack<K>();

            //Create a dummy edge to start the search
            dummy = Activator.CreateInstance<K>();
            dummy.Start = source;
            dummy.End = source;
            stackedEdges.Push(dummy);
        }

        public override SearchState Search()
        {
            K next;
            List<K> nodeEdges;

            //While there are stacked edges
            while (stackedEdges.Count != 0)
            {
                next = stackedEdges.Pop();

                route[next.End] = next.Start;
                visited[next.End] = Visibility.Visited;

                //If the edge ends in the target, return success
                if (next.End == target)
                {
                    found = SearchState.CompletedAndFound;
                    return found;
                }

                //Stack all child edges from this node that aren´t visited
                nodeEdges = graph.GetEdgesFromNode(next.End);
                for (int i = 0; i < nodeEdges.Count; i++)
                    if (visited[nodeEdges[i].End] == Visibility.Unvisited)
                        stackedEdges.Push(nodeEdges[i]);
            }

            //The search failed
            found = SearchState.CompletedAndNotFound;
            return found;
        }

        public override SearchState CycleOnce()
        {
            K next;
            List<K> nodeEdges;

            //If the stack is empty, the search was a failure
            if (stackedEdges.Count == 0)
            {
                found = SearchState.CompletedAndNotFound;
                return found;
            }

            //Pop an edge
            next = stackedEdges.Pop();

            route[next.End] = next.Start;
            visited[next.End] = Visibility.Visited;

            //If the edge ends in the target, return success
            if (next.End == target)
            {
                found = SearchState.CompletedAndFound;
                return found;
            }

            //Stack all child edges from this node that aren´t visited
            nodeEdges = graph.GetEdgesFromNode(next.End);
            for (int i = 0; i < nodeEdges.Count; i++)
                if (visited[nodeEdges[i].End] == Visibility.Unvisited)
                    stackedEdges.Push(nodeEdges[i]);

            found = SearchState.NotCompleted;
            return found;
        }

    }
}
