using CasaEngine.Core.Logger;

namespace CasaEngine.Framework.AI.StateMachines;

[Serializable]
public class FiniteStateMachine<T> : IFiniteStateMachine<T> where T : IFsmCapable<T>
{
    protected T Owner;

    protected IState<T> _currentState;

    protected IState<T> _previousState;

    protected IState<T> _globalState;

    public FiniteStateMachine(T owner)
    {
        Owner = owner;

        _currentState = new DefaultIdleState<T>();
        _previousState = new DefaultIdleState<T>();
        _globalState = new DefaultIdleState<T>();
    }

    public FiniteStateMachine(T owner, IState<T> currentState, IState<T> globalState)
    {
        Owner = owner;

        _currentState = currentState;
        _globalState = globalState;
        _previousState = new DefaultIdleState<T>();
    }

    public IState<T> CurrentState
    {
        get => _currentState;
        set => _currentState = value;
    }

    public IState<T> GlobalState
    {
        get => _globalState;
        set => _globalState = value;
    }

    public void Update(float elapsedTime)
    {
        _globalState.Update(Owner, elapsedTime);
        _currentState.Update(Owner, elapsedTime);
    }

    public void Transition(IState<T> newState)
    {
        LogManager.Instance.WriteLineTrace($"FSM: transition {_currentState.Name} -> {newState.Name}");

        _currentState.Exit(Owner);

        _previousState = _currentState;
        _currentState = newState;

        _currentState.Enter(Owner);
    }

    public void RevertStateChange()
    {
        Transition(_previousState);
    }

    public bool IsInState(IState<T> state)
    {
        if (_currentState == state)
        {
            return true;
        }

        else
        {
            return false;
        }
    }

    public bool HandleMessage(Message message)
    {
        if (_currentState.HandleMessage(Owner, message))
        {
            return true;
        }

        if (_globalState.HandleMessage(Owner, message))
        {
            return true;
        }

        return false;
    }
}