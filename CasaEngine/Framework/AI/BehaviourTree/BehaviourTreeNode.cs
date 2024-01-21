using CasaEngine.Framework.SceneManagement;

namespace CasaEngine.Framework.AI.BehaviourTree;

public abstract class BehaviourTreeNode<T> : AActor
{
    private readonly string _name;
    private readonly BehaviourTreeNode<T> _parent = null;

    private readonly List<BehaviourTreeNode<T>> _children = new();
    //condition script

    public string Name => _name;

    public BehaviourTreeNode<T> Parent => _parent;

    public List<BehaviourTreeNode<T>> Children => _children;

    public BehaviourTreeNode(string name)
    {
        _name = name;
    }

    public abstract bool EnterCondition(T entity);

    public abstract void Enter(T entity);

    public abstract void Exit(T entity);

    public abstract void Update(T entity);

    public abstract bool HandleMessage(T entity, Message message);

}