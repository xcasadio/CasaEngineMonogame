using CasaEngine.AI.Graphs;


namespace CasaEngine.AI.Pathfinding
{
    public class WidthFirstSearch<T, K> : GraphSearchAlgorithm<T, K>
        where T : Node
        where K : Edge
    {

        protected internal List<Visibility> visited;

        protected internal Queue<K> queuedEdges;

        protected internal List<int> route;



        public WidthFirstSearch(Graph<T, K> graph) : base(graph)
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

            //Initialize the base
            base.Initialize(source, target);

            visited = new List<Visibility>();
            route = new List<int>();

            //Initialize list values
            for (int i = 0; i < graph.NodeCount; i++)
            {
                visited.Add(Visibility.Unvisited);
                route.Add(Node.NoParent);
            }

            //Initialize the edges queue
            queuedEdges = new Queue<K>();

            //Create a dummy edge that will start the search
            dummy = Activator.CreateInstance<K>();
            dummy.Start = source;
            dummy.End = source;
            queuedEdges.Enqueue(dummy);
            visited[source] = Visibility.Visited;
        }

        public override SearchState Search()
        {
            K next;
            List<K> nodeEdges;

            //While there are enqueued edges
            while (queuedEdges.Count != 0)
            {
                next = queuedEdges.Dequeue();

                route[next.End] = next.Start;

                //If the edge ends in the target, return success
                if (next.End == target)
                {
                    found = SearchState.CompletedAndFound;
                    return found;
                }

                //Enqueue all child edges from this node that aren´t visited
                nodeEdges = graph.GetEdgesFromNode(next.End);
                for (int i = 0; i < nodeEdges.Count; i++)
                    if (visited[nodeEdges[i].End] == Visibility.Unvisited)
                    {
                        queuedEdges.Enqueue(nodeEdges[i]);
                        visited[next.End] = Visibility.Visited;
                    }
            }

            //The search failed
            found = SearchState.CompletedAndNotFound;
            return found;
        }

        public override SearchState CycleOnce()
        {
            K next;
            List<K> nodeEdges;

            //If the queue is empty, the search was a failure
            if (queuedEdges.Count == 0)
            {
                found = SearchState.CompletedAndNotFound;
                return found;
            }

            //Dequeue an edge
            next = queuedEdges.Dequeue();

            route[next.End] = next.Start;

            //If the edge ends in the target, return success
            if (next.End == target)
            {
                found = SearchState.CompletedAndFound;
                return found;
            }

            //Enqueue all child edges from this node that aren´t visited
            nodeEdges = graph.GetEdgesFromNode(next.End);
            for (int i = 0; i < nodeEdges.Count; i++)
                if (visited[nodeEdges[i].End] == Visibility.Unvisited)
                {
                    queuedEdges.Enqueue(nodeEdges[i]);
                    visited[next.End] = Visibility.Visited;
                }

            found = SearchState.NotCompleted;
            return found;
        }

    }
}
