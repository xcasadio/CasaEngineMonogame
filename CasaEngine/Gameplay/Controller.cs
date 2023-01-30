using CasaEngine.AI.StateMachines;

namespace CasaEngine.Gameplay
{
    public abstract class Controller
        : IFSMCapable<Controller>
    {

        private readonly FiniteStateMachine<Controller> m_FSM;
        private readonly Dictionary<int, IState<Controller>> m_States = new Dictionary<int, IState<Controller>>();
        private CharacterActor2D m_Character;



        public IFiniteStateMachine<Controller> StateMachine
        {
            get => m_FSM;
            set => throw new NotImplementedException();
        }

        public CharacterActor2D Character
        {
            get => m_Character;
            set => m_Character = value;
        }



        protected Controller(CharacterActor2D character_)
        {
            m_FSM = new FiniteStateMachine<Controller>(this);
            Character = character_;
        }



        public IState<Controller> GetState(int stateId_)
        {
            return m_States[stateId_];
        }

        public void AddState(int stateId_, IState<Controller> state_)
        {
            m_States.Add(stateId_, state_);
        }

        public abstract void Initialize();

        public virtual void Update(float elapsedTime_)
        {
            m_FSM.Update(elapsedTime_);
        }

        /*public override string ToString()
        {
            return m_FSM.CurrentState.ToString();
        }*/

    }
}
