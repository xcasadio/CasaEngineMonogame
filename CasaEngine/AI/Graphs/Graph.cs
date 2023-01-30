using Microsoft.Xna.Framework;




namespace CasaEngine.AI.Graphs
{
    [Serializable]
    public class Graph<T, K>
        where T : Node
        where K : Edge
    {

        protected internal int nextNode;

        protected internal List<T> nodes;

        protected internal List<List<K>> edges;

        protected internal bool digraph;



        public Graph(bool digraph)
        {
            this.digraph = digraph;

            nodes = new List<T>();
            edges = new List<List<K>>();
        }



        public int NodeCount => nodes.Count;

        public int ActiveNodeCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < nodes.Count; i++)
                    if (nodes[i].Index != Edge.InvalidNode)
                        count++;

                return count;
            }
        }

        public int EdgesCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < edges.Count; i++)
                    count += edges[i].Count;

                return count;
            }
        }

        public int ActiveEdgesCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < edges.Count; i++)
                    for (int j = 0; j < edges[i].Count; j++)
                        if ((nodes[edges[i][j].Start].Index != Edge.InvalidNode) && (nodes[edges[i][j].End].Index != Edge.InvalidNode))
                            count++;

                return count;
            }
        }



        public bool IsNodeActive(int index)
        {
            if (index < 0 || index >= nodes.Count)
                return false;

            if (nodes[index].Index == Edge.InvalidNode)
                return false;

            return true;
        }

        public bool IsEdgePresent(int start, int end)
        {
            if (IsNodeActive(start) && IsNodeActive(end))
                for (int i = 0; i < edges[start].Count; i++)
                    if (edges[start][i].End == end)
                        return true;

            return false;
        }

        public T GetNode(int index)
        {
            if (index < 0 || index > nodes.Count)
                throw new Exception("Invalid index");

            return nodes[index];
        }

        public K GetEdge(int start, int end)
        {
            if (IsNodeActive(start) && IsNodeActive(end))
                for (int i = 0; i < edges[start].Count; i++)
                    if (edges[start][i].End == end)
                        return edges[start][i];

            throw new Exception("Invalid edge");
        }

        public List<K> GetEdgesFromNode(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex > nodes.Count)
                throw new Exception("Invalid index");

            return edges[nodeIndex];
        }

        public void AddEdge(K edge)
        {
            K reverseEdge;

            if (IsNodeActive(edge.Start) && IsNodeActive(edge.End))
                if (IsEdgePresent(edge.Start, edge.End) == false)
                    edges[edge.Start].Add(edge);

            //If the graph is a digraph, add the reversed edge
            if (digraph)
                if (IsEdgePresent(edge.End, edge.Start) == false)
                {
                    reverseEdge = (K)edge.Clone();
                    reverseEdge.Start = edge.End;
                    reverseEdge.End = edge.Start;

                    edges[edge.End].Add(reverseEdge);
                }
        }

        public void RemoveEdge(int start, int end)
        {
            if (IsNodeActive(start) && IsNodeActive(end))
            {
                for (int i = 0; i < edges[start].Count; i++)
                    if (edges[start][i].End == end)
                    {
                        edges[start].Remove(edges[start][i]);
                        break;
                    }

                //If the graph is a digraph remove the revesed edge
                if (digraph)
                    for (int i = 0; i < edges[end].Count; i++)
                        if (edges[end][i].End == start)
                        {
                            edges[end].Remove(edges[end][i]);
                            break;
                        }

            }
        }

        public void AddNode(T node)
        {
            if (node.Index == Edge.InvalidNode)
            {
                node.Index = nextNode++;
                nodes.Add(node);
                edges.Add(new List<K>());
            }

            else
                throw new Exception("A node can´t have an index before being added to a graph");
        }

        public void RemoveNode(T node)
        {
            int nodeIndex = nodes.IndexOf(node);
            List<K> toDelete = new List<K>();

            edges.RemoveAt(nodeIndex);
            nodes.Remove(node);

            foreach (List<K> edgeList in edges)
            {
                toDelete.Clear();

                foreach (K edge in edgeList)
                {
                    if (edge.Start == nodeIndex || edge.End == nodeIndex)
                    {
                        toDelete.Add(edge);
                    }
                }

                foreach (K edge in toDelete)
                {
                    edgeList.Remove(edge);
                }
            }
        }

        public List<T> GetNeighbourNodes(int spacePartitionSector, Vector3 searchPosition, float searchRange)
        {
            List<T> neighbours;

            neighbours = new List<T>();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].IsNeighbour(spacePartitionSector, searchPosition, searchRange) == true)
                    neighbours.Add(nodes[i]);

            return neighbours;
        }

    }
}
