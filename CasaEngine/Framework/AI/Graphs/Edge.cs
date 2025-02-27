using System.Runtime.Serialization.Formatters.Binary;

namespace CasaEngine.Framework.AI.Graphs;

[Serializable]
public class Edge : ICloneable
{

    public const int InvalidNode = -1;

    protected internal int start;

    protected internal int end;

    public Edge()
    {
        start = InvalidNode;
        end = InvalidNode;
    }

    public Edge(int start, int end)
    {
        var message = string.Empty;

        if (ValidateNode(start, ref message) == false)
        {
            throw new AiException("start", GetType().ToString(), message);
        }

        if (ValidateNode(end, ref message) == false)
        {
            throw new AiException("end", GetType().ToString(), message);
        }

        this.start = start;
        this.end = end;
    }

    public int Start
    {
        get => start;
        set
        {
            var message = string.Empty;

            if (ValidateNode(value, ref message) == false)
            {
                throw new AiException("start", GetType().ToString(), message);
            }

            start = value;
        }
    }

    public int End
    {
        get => end;
        set
        {

            var message = string.Empty;

            if (ValidateNode(value, ref message) == false)
            {
                throw new AiException("end", GetType().ToString(), message);
            }

            end = value;
        }
    }

    public static bool ValidateNode(int index, ref string message)
    {
        if (index < InvalidNode)
        {
            message = "The node index number must be equal or greater than " + InvalidNode;
            return false;
        }

        return true;
    }

    public object Clone()
    {
        return (Edge)MemberwiseClone();
    }

}