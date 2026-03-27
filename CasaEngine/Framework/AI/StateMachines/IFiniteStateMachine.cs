using CasaEngine.Framework.AI.Messaging;

namespace CasaEngine.Framework.AI.StateMachines;

public interface IFiniteStateMachine<T> : IMessageable
{
    IState<T> CurrentState { get; set; }
    IState<T> GlobalState { get; set; }

    void Update(float elapsedTime);
    void Transition(IState<T> newState);
    void RevertStateChange();
    bool IsInState(IState<T> state);
}