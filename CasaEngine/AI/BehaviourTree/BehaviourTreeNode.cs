namespace CasaEngine.AI.BehaviourTree
{
    public abstract class BehaviourTreeNode<T>
        : BaseEntity
    {
        readonly string m_Name;
        readonly BehaviourTreeNode<T> m_Parent = null;

        readonly List<BehaviourTreeNode<T>> m_Children = new List<BehaviourTreeNode<T>>();
        //condition script



        public string Name => m_Name;

        public BehaviourTreeNode<T> Parent => m_Parent;

        public List<BehaviourTreeNode<T>> Children => m_Children;


        public BehaviourTreeNode(string name_)
        {
            m_Name = name_;
        }



        public abstract bool EnterCondition(T entity);

        public abstract void Enter(T entity);

        public abstract void Exit(T entity);

        public abstract void Update(T entity);

        public abstract bool HandleMessage(T entity, Message message);

    }
}
