using CasaEngine.Core.Collections;
using CasaEngine.Framework.AI.Graphs;

namespace CasaEngine.Framework.AI.Pathfinding;

public class AStarSearch<T, TK> : GraphSearchAlgorithm<T, TK>
    where T : NavigationNode
    where TK : WeightedEdge
{

    protected internal List<double> FCosts;

    protected internal List<double> GCosts;

    protected internal List<TK> ShortestPathTree;

    protected internal List<TK> Frontier;

    protected internal HeuristicMethod<T, TK> Heuristic;

    protected internal IndexedPriorityQueue<double> Queue;



    public AStarSearch(Graph<T, TK> graph, HeuristicMethod<T, TK> heuristic) : base(graph)
    {
        Heuristic = heuristic;
    }



    public override List<int> PathOfNodes
    {
        get
        {
            List<int> path;
            int aux;

            //If the search didn�t find anything, return an empty path
            path = new List<int>();
            if (Found != SearchState.CompletedAndFound)
            {
                return path;
            }

            //Get the list of nodes from source to target. The shortest patrh tree is visited in reverse order
            path.Insert(0, Target);
            aux = Target;
            while (aux != Source)
            {
                aux = ShortestPathTree[aux].Start;
                path.Insert(0, aux);
            }

            return path;
        }
    }

    public override List<TK> PathOfEdges
    {
        get
        {
            List<TK> path;
            int aux;

            //If the search didn�t find anything, return an empty path
            path = new List<TK>();
            if (Found != SearchState.CompletedAndFound)
            {
                return path;
            }

            //Get the list of edges from source to target. The shortest patrh tree is visited in reverse order
            aux = Target;
            while (aux != Source)
            {
                path.Insert(0, ShortestPathTree[aux]);
                aux = ShortestPathTree[aux].start;
            }

            return path;
        }
    }



    public override void Initialize(int source, int target)
    {
        //Create the auxiliary lists
        FCosts = new List<double>(Graph.NodeCount);
        GCosts = new List<double>(Graph.NodeCount);
        ShortestPathTree = new List<TK>(Graph.NodeCount);
        Frontier = new List<TK>(Graph.NodeCount);

        //LoadContent the values
        for (var i = 0; i < Graph.NodeCount; i++)
        {
            FCosts.Add(0.0);
            GCosts.Add(0.0);
            ShortestPathTree.Add(default);
            Frontier.Add(default);
        }

        //LoadContent the indexed priority queue and enqueue the start node
        Queue = new IndexedPriorityQueue<double>(FCosts);
        Queue.Enqueue(source);

        //LoadContent the base
        base.Initialize(source, target);
    }

    public override SearchState Search()
    {
        int closestNode;
        List<TK> edges;
        double hCost, gCost;

        while (Queue.Count != 0)
        {
            //Dequeue the first node (the one with the lower cost) and add it to the shortest path tree
            closestNode = Queue.Dequeue();
            ShortestPathTree[closestNode] = Frontier[closestNode];

            //If it�s the target, return
            if (closestNode == Target)
            {
                Found = SearchState.CompletedAndFound;
                return Found;
            }

            //Get all edges that start from the current node
            edges = Graph.GetEdgesFromNode(closestNode);
            for (var i = 0; i < edges.Count; i++)
            {
                //Calculate the estimated cost to reach the target from the next node
                hCost = Heuristic(Graph, Target, edges[i].End);

                //Calculate the cost to reach the next node with the current one and the edge
                gCost = GCosts[closestNode] + edges[i].Cost;

                //If the node wasn�t in the frontier, add it
                if (Frontier[edges[i].End] == null)
                {
                    FCosts[edges[i].End] = gCost + hCost;
                    GCosts[edges[i].End] = gCost;

                    Queue.Enqueue(edges[i].End);
                    Frontier[edges[i].End] = edges[i];
                }

                else //If it was, see if the new way of reaching the node is shorter or not. If it�s shorter, update the cost to reach it
                {
                    if (gCost < GCosts[edges[i].End] && ShortestPathTree[edges[i].End] == null)
                    {
                        FCosts[edges[i].End] = gCost + hCost;
                        GCosts[edges[i].End] = gCost;

                        Queue.ChangePriority(edges[i].End);
                        Frontier[edges[i].End] = edges[i];
                    }
                }
            }
        }

        Found = SearchState.CompletedAndNotFound;
        return Found;
    }

    public override SearchState CycleOnce()
    {
        int closestNode;
        List<TK> edges;
        double hCost, gCost;

        if (Queue.Count == 0)
        {
            Found = SearchState.CompletedAndNotFound;
            return Found;
        }

        //Dequeue the first node (the one with the lower cost) and add it to the shortest path tree
        closestNode = Queue.Dequeue();
        ShortestPathTree[closestNode] = Frontier[closestNode];

        //If it�s the target, return
        if (closestNode == Target)
        {
            Found = SearchState.CompletedAndFound;
            return Found;
        }

        //Get all edges that start from the current node
        edges = Graph.GetEdgesFromNode(closestNode);
        for (var i = 0; i < edges.Count; i++)
        {
            //Calculate the estimated cost to reach the target from the next node
            hCost = Heuristic(Graph, Target, edges[i].End);

            //Calculate the cost to reach the next node with the current one and the edge
            gCost = GCosts[closestNode] + edges[i].Cost;

            //If the node wasn�t in the frontier, add it
            if (Frontier[edges[i].End] == null)
            {
                FCosts[edges[i].End] = gCost + hCost;
                GCosts[edges[i].End] = gCost;

                Queue.Enqueue(edges[i].End);
                Frontier[edges[i].End] = edges[i];
            }

            else //If it was, see if the new way of reaching the node is shorter or not. If it�s shorter, update the cost to reach it
            {
                if (gCost < GCosts[edges[i].End] && ShortestPathTree[edges[i].End] == null)
                {
                    FCosts[edges[i].End] = gCost + hCost;
                    GCosts[edges[i].End] = gCost;

                    Queue.ChangePriority(edges[i].End);
                    Frontier[edges[i].End] = edges[i];
                }
            }
        }

        Found = SearchState.NotCompleted;
        return Found;
    }

}