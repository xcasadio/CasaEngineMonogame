namespace CasaEngine.AI.StateMachines
{
    [Serializable]
    public class FiniteStateMachine<T> : IFiniteStateMachine<T> where T : /*BaseEntity,*/ IFSMCapable<T>
    {

        protected internal T owner;

        protected internal IState<T> currentState;

        protected internal IState<T> previousState;

        protected internal IState<T> globalState;



        public FiniteStateMachine(T owner)
        {
            this.owner = owner;

            currentState = new DefaultIdleState<T>();
            previousState = new DefaultIdleState<T>();
            globalState = new DefaultIdleState<T>();
        }

        public FiniteStateMachine(T owner, IState<T> currentState, IState<T> globalState)
        {
            this.owner = owner;

            this.currentState = currentState;
            this.globalState = globalState;
            this.previousState = new DefaultIdleState<T>();
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



        public void Update(float elpasedTime_)
        {
            globalState.Update(owner, elpasedTime_);
            currentState.Update(owner, elpasedTime_);
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
            currentState.Exit(owner);

            //Actualize internal values
            previousState = currentState;
            currentState = newState;

            //Enter the new state
            currentState.Enter(owner);
        }

        public void RevertStateChange()
        {
            Transition(previousState);
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
            if (currentState.HandleMessage(owner, message) == true)
                return true;

            //If the current state couldn´t handle the message, try the global state
            if (globalState.HandleMessage(owner, message) == true)
                return true;

            //The machine wasn´t able to handle the message
            return false;
        }

    }
}
