using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.AI.BehaviourTree;

public class BehaviourTree<T> where T : AActor
{
    protected internal T Owner;

    private BehaviourTreeNode<T> _root;
    private BehaviourTreeNode<T> _currentNode;

    public void Update()
    {
        if (_root == null)
        {
            return;
        }

        if (_root.EnterCondition(Owner) == false)
        {
            _currentNode?.Exit(Owner);

            _currentNode = null; //rester dans l'etat ??
        }

        var node = Update(_root.Children);

        if (node == null)
        {
            _currentNode?.Exit(Owner);

            _currentNode = null;
        }
        else
        {
            if (_currentNode != node)
            {
                _currentNode.Exit(Owner);
                node.Enter(Owner);
                _currentNode = node;
            }
        }
    }

    private BehaviourTreeNode<T> Update(List<BehaviourTreeNode<T>> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.EnterCondition(Owner))
            {
                if (node.Children.Count == 0)
                {
                    return node;
                }

                return Update(node.Children);
            }
        }

        return null;
    }

    public BehaviourTreeNode<T> GetNodeByName(string name)
    {
        if (_root == null)
        {
            return null;
        }

        if (_root.Name.Equals(name))
        {
            return _root;
        }

        return SearchNodeByName(name, _root.Children);
    }

    private BehaviourTreeNode<T> SearchNodeByName(string name, List<BehaviourTreeNode<T>> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Name.Equals(name))
            {
                return node;
            }

            return SearchNodeByName(name, node.Children);
        }

        return null;
    }

    public void AddNode(string parentNodeName, BehaviourTreeNode<T> node)
    {
        if (node == null)
        {
            throw new ArgumentNullException("BehaviourTree<T>.AddNode() : node is null");
        }

        if (string.IsNullOrEmpty(parentNodeName))
        {
            _root = node;
        }

        var parent = GetNodeByName(parentNodeName);

        if (parent == null)
        {
            throw new InvalidOperationException("BehaviourTree<T>.AddNode() : node named " + parentNodeName + " is unknown");
        }

        parent.Children.Add(node);
    }
}