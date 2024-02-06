using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.AI.BehaviourTree;

public interface IBehaviourTreeCapable<T> where T : AActor, IBehaviourTreeCapable<T>
{
    BehaviourTree<T> StateMachine { get; set; }
}