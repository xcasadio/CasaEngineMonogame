using System;
using System.Collections.Generic;
using CasaEngine.Framework.AI.StateMachines;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;

namespace CasaEngine.RPGDemo.Controllers;

public abstract class Controller : IFsmCapable<Controller>
{
    private readonly FiniteStateMachine<Controller> _fsm;
    private readonly Dictionary<int, IState<Controller>> _states = new();
    private bool _isInitialized;

    public IFiniteStateMachine<Controller> StateMachine
    {
        get => _fsm;
        set => throw new NotImplementedException();
    }

    public Character Character { get; }

    protected Controller(Character character)
    {
        _fsm = new FiniteStateMachine<Controller>(this);
        Character = character;
    }

    public IState<Controller> GetState(int stateId)
    {
        return _states[stateId];
    }

    public void AddState(int stateId, IState<Controller> state)
    {
        _states.Add(stateId, state);
    }

    public void Initialize(World world)
    {
        if (_isInitialized)
        {
            return;
        }

        InitializePrivate(world);

        _isInitialized = true;
    }

    public abstract void InitializePrivate(World world);

    public virtual void Update(float elapsedTime)
    {
        _fsm.Update(elapsedTime);
    }
}