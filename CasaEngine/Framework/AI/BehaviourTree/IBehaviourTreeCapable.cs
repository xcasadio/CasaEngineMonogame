using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.AI.BehaviourTree;

public interface IBehaviourTreeCapable<T> where T : Entity, IBehaviourTreeCapable<T>
{
    BehaviourTree<T> StateMachine { get; set; }
}