namespace CasaEngine.AI.StateMachines
{
    [Serializable]
    public class FiniteStateMachine<T> : IFiniteStateMachine<T> where T : /*BaseEntity,*/ IFsmCapable<T>
    {

        protected internal T Owner;

        protected internal IState<T> CurrentState;

        protected internal IState<T> PreviousState;

        protected internal IState<T> GlobalState;



        public FiniteStateMachine(T owner)
        {
            this.Owner = owner;

            currentState = new DefaultIdleState<T>();
            PreviousState = new DefaultIdleState<T>();
            globalState = new DefaultIdleState<T>();
        }

        public FiniteStateMachine(T owner, IState<T> currentState, IState<T> globalState)
        {
            this.Owner = owner;

            this.currentState = currentState;
            this.globalState = globalState;
            this.PreviousState = new DefaultIdleState<T>();
        }



        public IState<T> CurrentState
        {
            get => currentState;
            set => this.currentState = value;
        }

        public IState<T> GlobalState
        {
            get => globalState;
            set => this.globalState = value;
        }



        public void Update(float elpasedTime)
        {
            globalState.Update(Owner, elpasedTime);
            currentState.Update(Owner, elpasedTime);
        }

        public void Transition(IState<T> newState)
        {
            /*String message = String.Empty;

            if (LogManager.Instance.Verbosity == LogManager.LogVerbosity.Debug)
            {
                string n;

                if (owner is BaseObject)
                {
                    n = (owner as BaseObject).Name;
                }
                else
                {
                    n = "unknown";
                }

                if (LogManager.Instance.Verbosity == LogManager.LogVerbosity.Debug)
                {
                    LogManager.Instance.WriteLineDebug("Object '" + n + "' change state '" + currentState.Name + "' to '" + newState.Name + "'");
                }
            }*/

            //Exit the actual state
            currentState.Exit(Owner);

            //Actualize internal values
            PreviousState = currentState;
            currentState = newState;

            //Enter the new state
            currentState.Enter(Owner);
        }

        public void RevertStateChange()
        {
            Transition(PreviousState);
        }

        public bool IsInState(IState<T> state)
        {
            if (this.currentState == state)
                return true;

            else
                return false;
        }

        public bool HandleMessage(Message message)
        {
            //Try to handle the message with the current state
            if (currentState.HandleMessage(Owner, message) == true)
                return true;

            //If the current state couldn´t handle the message, try the global state
            if (globalState.HandleMessage(Owner, message) == true)
                return true;

            //The machine wasn´t able to handle the message
            return false;
        }

    }
}
