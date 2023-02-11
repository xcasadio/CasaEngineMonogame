namespace CasaEngine.AI.BehaviourTree
{
    public interface IBehaviourTreeCapable<T> where T : BaseEntity, IBehaviourTreeCapable<T>
    {
        BehaviourTree<T> StateMachine
        {
            get;
            set;
        }
    }
}
