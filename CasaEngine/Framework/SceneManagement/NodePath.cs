namespace CasaEngine.Framework.SceneManagement;

public class NodePath : LinkedList<INode>
{
    public NodePath Copy()
    {
        var result = new NodePath();
        foreach (var node in this)
        {
            result.AddLast(node);
        }

        return result;
    }
}