using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Graphs;

[Serializable]
public class Node : ICloneable
{
    public const int NoParent = -1;

    protected internal int index;

    public Node()
    {
        index = Edge.InvalidNode;
    }

    public Node(int index)
    {
        var message = string.Empty;

        if (ValidateIndex(index, ref message) == false)
        {
            throw new AiException("index", GetType().ToString(), message);
        }

        this.index = index;
    }

    public int Index
    {
        get => index;
        protected internal set => index = value;
    }

    public static bool ValidateIndex(int index, ref string message)
    {
        if (index < Edge.InvalidNode)
        {
            message = "The index value of the node can't be less than " + Edge.InvalidNode;
            return false;
        }

        return true;
    }

    public object Clone()
    {
        return (Node)MemberwiseClone();
    }

    protected internal virtual bool IsNeighbour(int spacePartitionSector, Vector3 searchPosition, float searchRange)
    {
        return true;
    }
}