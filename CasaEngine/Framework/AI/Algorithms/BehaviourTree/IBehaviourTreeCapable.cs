using CasaEngine.Framework.Scene.Entities;

namespace CasaEngine.Framework.AI.Algorithms.BehaviourTree;

public interface IBehaviourTreeCapable<T> where T : Entity, IBehaviourTreeCapable<T>
{
    BehaviourTree<T> StateMachine { get; set; }
}