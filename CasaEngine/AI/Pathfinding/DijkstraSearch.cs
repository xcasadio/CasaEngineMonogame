using CasaEngineCommon.Collection;

using CasaEngine.AI.Graphs;


namespace CasaEngine.AI.Pathfinding
{
    public class DijkstraSearch<T, K> : GraphSearchAlgorithm<T, K>
        where T : Node
        where K : WeightedEdge
    {

        protected internal List<double> costToNode;

        protected internal List<K> shortestPathTree;

        protected internal List<K> frontier;

        protected internal IndexedPriorityQueue<double> queue;



        public DijkstraSearch(Graph<T, K> graph) : base(graph)
        { }



        public override List<int> PathOfNodes
        {
            get
            {
                List<int> path;
                int aux;

                //If the search didn´t find anything, return an empty path
                path = new List<int>();
                if (found != SearchState.CompletedAndFound)
                    return path;

                //Get the list of nodes from source to target. The shortest patrh tree is visited in reverse order
                path.Insert(0, target);
                aux = target;
                while (aux != source)
                {
                    aux = shortestPathTree[aux].Start;
                    path.Insert(0, aux);
                }

                return path;
            }
        }

        public override List<K> PathOfEdges
        {
            get
            {
                List<K> path;
                int aux;

                //If the search didn´t find anything, return an empty path
                path = new List<K>();
                if (found != SearchState.CompletedAndFound)
                    return path;

                //Get the list of edges from source to target. The shortest patrh tree is visited in reverse order
                aux = target;
                while (aux != source)
                {
                    path.Insert(0, shortestPathTree[aux]);
                    aux = shortestPathTree[aux].start;
                }

                return path;
            }
        }



        public override void Initialize(int source, int target)
        {
            //Create the auxiliary lists
            costToNode = new List<double>(graph.NodeCount);
            shortestPathTree = new List<K>(graph.NodeCount);
            frontier = new List<K>(graph.NodeCount);

            //Initialize the values
            for (int i = 0; i < graph.NodeCount; i++)
            {
                costToNode.Add(Double.MaxValue);
                shortestPathTree.Add(default(K));
                frontier.Add(default(K));
            }

            costToNode[source] = 0;

            //Initialize the indexed priority queue and enqueue the first node
            queue = new IndexedPriorityQueue<double>(costToNode);
            queue.Enqueue(source);

            //Initialize the base
            base.Initialize(source, target);
        }

        public override SearchState Search()
        {
            int closestNode;
            List<K> edges;
            double newCost;

            while (queue.Count != 0)
            {
                //Dequeue the first node (the one with the lower cost) and add it to the shortest path tree
                closestNode = queue.Dequeue();
                shortestPathTree[closestNode] = frontier[closestNode];

                //If it´s the target, return
                if (closestNode == target)
                {
                    found = SearchState.CompletedAndFound;
                    return found;
                }

                //Get all edges that start from the current node
                edges = graph.GetEdgesFromNode(closestNode);
                for (int i = 0; i < edges.Count; i++)
                {
                    //Calculate the cost to reach the next node with the current one and the edge
                    newCost = costToNode[closestNode] + edges[i].Cost;

                    //If the node wasn´t in the frontier, add it
                    if (frontier[edges[i].End] == null)
                    {
                        costToNode[edges[i].End] = newCost;
                        queue.Enqueue(edges[i].End);
                        frontier[edges[i].End] = edges[i];
                    }

                    else //If it was, see if the new way of reaching the node is shorter or not. If it´s shorter, update the cost to reach it
                    {
                        if ((newCost < costToNode[edges[i].End]) && (shortestPathTree[edges[i].End] == null))
                        {
                            costToNode[edges[i].End] = newCost;
                            queue.ChangePriority(edges[i].End);
                            frontier[edges[i].End] = edges[i];
                        }
                    }
                }
            }

            found = SearchState.CompletedAndNotFound;
            return found;
        }

        public override SearchState CycleOnce()
        {
            int closestNode;
            List<K> edges;
            double newCost;

            //If there aren´t nodes left in the queue, the search failed
            if (queue.Count == 0)
            {
                found = SearchState.CompletedAndNotFound;
                return found;
            }

            //Dequeue the first node (the one with the lower cost) and add it to the shortest path tree
            closestNode = queue.Dequeue();
            shortestPathTree[closestNode] = frontier[closestNode];

            //If it´s the target, return
            if (closestNode == target)
            {
                found = SearchState.CompletedAndFound;
                return found;
            }

            //Get all edges that start from the current node
            edges = graph.GetEdgesFromNode(closestNode);
            for (int i = 0; i < edges.Count; i++)
            {
                //Calculate the cost to reach the next node with the current one and the edge
                newCost = costToNode[closestNode] + edges[i].Cost;

                //If the node wasn´t in the frontier, add it
                if (frontier[edges[i].End] == null)
                {
                    costToNode[edges[i].End] = newCost;
                    queue.Enqueue(edges[i].End);
                    frontier[edges[i].End] = edges[i];
                }

                else //If it was, see if the new way of reaching the node is shorter or not. If it´s shorter, update the cost to reach it
                {
                    if ((newCost < costToNode[edges[i].End]) && (shortestPathTree[edges[i].End] == null))
                    {
                        costToNode[edges[i].End] = newCost;
                        queue.ChangePriority(edges[i].End);
                        frontier[edges[i].End] = edges[i];
                    }
                }
            }

            found = SearchState.NotCompleted;
            return found;
        }

    }
}
