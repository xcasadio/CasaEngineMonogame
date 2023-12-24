using System.Collections.Generic;

namespace Veldrid.SceneGraph;

public class CollectParentPaths : NodeVisitor
{
    private INode _haltTraversalAtNode;
    private List<LinkedList<INode>> _nodePaths;

    public IReadOnlyList<LinkedList<INode>> NodePaths
    {
        get => _nodePaths;
    }

    public CollectParentPaths(INode haltTraversalAtNode = null) :
        base(VisitorType.NodeVisitor, TraversalModeType.TraverseParents)
    {
        _haltTraversalAtNode = haltTraversalAtNode;
        _nodePaths = new List<LinkedList<INode>>();
    }

    public override void Apply(INode node)
    {
        if (node.NumParents == 0 || node == _haltTraversalAtNode)
        {
            _nodePaths.Add(NodePath);
        }
        else
        {
            Traverse(node);
        }
    }
}