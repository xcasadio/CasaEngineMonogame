using System;
using System.Collections.Generic;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;

namespace RPGDemo.Controllers;

public abstract class Controller : IFsmCapable<Controller>
{
    private readonly FiniteStateMachine<Controller> _fsm;
    private readonly Dictionary<int, IState<Controller>> _states = new();

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

    public virtual void Initialize(CasaEngineGame game)
    {
        Character.Initialize(game);
        Character.AnimatedSpriteComponent.AnimationFinished += OnAnimationFinished;
    }

    public virtual void Update(float elapsedTime)
    {
        _fsm.Update(elapsedTime);
        Character.Update(elapsedTime);
    }

    private void OnAnimationFinished(object sender, CasaEngine.Framework.Assets.Animations.Animation2d animation2d)
    {
        if (sender is Component component)
        {
            StateMachine.HandleMessage(new Message(component.Owner.Id, component.Owner.Id,
                (int)MessageType.AnimationChanged, 0.0f, animation2d));
        }
    }
}