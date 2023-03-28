using CasaEngine.Framework.AI.StateMachines;

namespace CasaEngine.Framework.Gameplay;

public abstract class Controller
    : IFsmCapable<Controller>
{

    private readonly FiniteStateMachine<Controller> _fsm;
    private readonly Dictionary<int, IState<Controller>> _states = new();
    private CharacterActor2D _character;



    public IFiniteStateMachine<Controller> StateMachine
    {
        get => _fsm;
        set => throw new NotImplementedException();
    }

    public CharacterActor2D Character
    {
        get => _character;
        set => _character = value;
    }



    protected Controller(CharacterActor2D character)
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

    public abstract void Initialize();

    public virtual void Update(float elapsedTime)
    {
        _fsm.Update(elapsedTime);
    }

    /*public override string ToString()
    {
        return _FSM.CurrentState.ToString();
    }*/

}