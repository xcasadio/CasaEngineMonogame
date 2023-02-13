using CasaEngine.Gameplay;
using CasaEngine.Gameplay.Actor;

namespace CasaEngine.AI.BehaviourTree
{
    public interface IBehaviourTreeCapable<T> where T : BaseObject, IBehaviourTreeCapable<T>
    {
        BehaviourTree<T> StateMachine { get; set; }
    }
}
