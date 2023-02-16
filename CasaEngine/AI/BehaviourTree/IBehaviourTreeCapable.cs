using CasaEngine.Entities;

namespace CasaEngine.AI.BehaviourTree
{
    public interface IBehaviourTreeCapable<T> where T : Entity, IBehaviourTreeCapable<T>
    {
        BehaviourTree<T> StateMachine { get; set; }
    }
}
