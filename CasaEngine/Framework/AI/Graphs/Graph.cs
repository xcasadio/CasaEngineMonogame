using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Graphs;

[Serializable]
public class Graph<T, TK>
    where T : Node
    where TK : Edge
{

    protected internal int NextNode;

    protected internal List<T> Nodes;

    protected internal List<List<TK>> Edges;

    protected internal bool Digraph;

    public Graph(bool digraph)
    {
        Digraph = digraph;

        Nodes = new List<T>();
        Edges = new List<List<TK>>();
    }

    public int NodeCount => Nodes.Count;

    public int ActiveNodeCount
    {
        get
        {
            var count = 0;

            for (var i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Index != Edge.InvalidNode)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int EdgesCount
    {
        get
        {
            var count = 0;

            for (var i = 0; i < Edges.Count; i++)
            {
                count += Edges[i].Count;
            }

            return count;
        }
    }

    public int ActiveEdgesCount
    {
        get
        {
            var count = 0;

            for (var i = 0; i < Edges.Count; i++)
            {
                for (var j = 0; j < Edges[i].Count; j++)
                {
                    if (Nodes[Edges[i][j].Start].Index != Edge.InvalidNode && Nodes[Edges[i][j].End].Index != Edge.InvalidNode)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }

    public bool IsNodeActive(int index)
    {
        if (index < 0 || index >= Nodes.Count)
        {
            return false;
        }

        if (Nodes[index].Index == Edge.InvalidNode)
        {
            return false;
        }

        return true;
    }

    public bool IsEdgePresent(int start, int end)
    {
        if (IsNodeActive(start) && IsNodeActive(end))
        {
            for (var i = 0; i < Edges[start].Count; i++)
            {
                if (Edges[start][i].End == end)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public T GetNode(int index)
    {
        if (index < 0 || index > Nodes.Count)
        {
            throw new Exception("Invalid index");
        }

        return Nodes[index];
    }

    public TK GetEdge(int start, int end)
    {
        if (IsNodeActive(start) && IsNodeActive(end))
        {
            for (var i = 0; i < Edges[start].Count; i++)
            {
                if (Edges[start][i].End == end)
                {
                    return Edges[start][i];
                }
            }
        }

        throw new Exception("Invalid edge");
    }

    public List<TK> GetEdgesFromNode(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex > Nodes.Count)
        {
            throw new Exception("Invalid index");
        }

        return Edges[nodeIndex];
    }

    public void AddEdge(TK edge)
    {
        TK reverseEdge;

        if (IsNodeActive(edge.Start) && IsNodeActive(edge.End))
        {
            if (IsEdgePresent(edge.Start, edge.End) == false)
            {
                Edges[edge.Start].Add(edge);
            }
        }

        //If the graph is a digraph, add the reversed edge
        if (Digraph)
        {
            if (IsEdgePresent(edge.End, edge.Start) == false)
            {
                reverseEdge = (TK)edge.Clone();
                reverseEdge.Start = edge.End;
                reverseEdge.End = edge.Start;

                Edges[edge.End].Add(reverseEdge);
            }
        }
    }

    public void RemoveEdge(int start, int end)
    {
        if (IsNodeActive(start) && IsNodeActive(end))
        {
            for (var i = 0; i < Edges[start].Count; i++)
            {
                if (Edges[start][i].End == end)
                {
                    Edges[start].Remove(Edges[start][i]);
                    break;
                }
            }

            //If the graph is a digraph remove the revesed edge
            if (Digraph)
            {
                for (var i = 0; i < Edges[end].Count; i++)
                {
                    if (Edges[end][i].End == start)
                    {
                        Edges[end].Remove(Edges[end][i]);
                        break;
                    }
                }
            }
        }
    }

    public void AddNode(T node)
    {
        if (node.Index == Edge.InvalidNode)
        {
            node.Index = NextNode++;
            Nodes.Add(node);
            Edges.Add(new List<TK>());
        }

        else
        {
            throw new Exception("A node can´t have an index before being added to a graph");
        }
    }

    public void RemoveNode(T node)
    {
        var nodeIndex = Nodes.IndexOf(node);
        var toDelete = new List<TK>();

        Edges.RemoveAt(nodeIndex);
        Nodes.Remove(node);

        foreach (var edgeList in Edges)
        {
            toDelete.Clear();

            foreach (var edge in edgeList)
            {
                if (edge.Start == nodeIndex || edge.End == nodeIndex)
                {
                    toDelete.Add(edge);
                }
            }

            foreach (var edge in toDelete)
            {
                edgeList.Remove(edge);
            }
        }
    }

    public List<T> GetNeighbourNodes(int spacePartitionSector, Vector3 searchPosition, float searchRange)
    {
        List<T> neighbours;

        neighbours = new List<T>();
        for (var i = 0; i < Nodes.Count; i++)
        {
            if (Nodes[i].IsNeighbour(spacePartitionSector, searchPosition, searchRange))
            {
                neighbours.Add(Nodes[i]);
            }
        }

        return neighbours;
    }

}