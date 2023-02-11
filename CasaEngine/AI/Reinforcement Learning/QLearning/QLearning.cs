namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
    public class QLearning
    {

        protected float Gamma = 0.8f;
        protected float Alpha = 0.9f;
        protected float Epsilon = 0.8f;

        public string State = "Idle";
        public string Action = "Idling";

        /*protected AgentInitInfo _Info; //< initialization info
		protected Approximator _Approximator; //< function approximator we are using
		protected Sensors _PrevState;                  //< previous state
		protected Actions _PrevAction;                 //< previous action taken
		protected Actions _newAction;           //< new action*/

        readonly QPolicy _policy = new QPolicy();

        List<KeyValuePair<string, string>> _pastActions = new List<KeyValuePair<string, string>>();
        readonly float[] _pastReward = new float[50];



        public float Epsilon
        {
            get => _Epsilon;
            set => _Epsilon = value;
        }

        public float Gamma
        {
            get => _Gamma;
            set => _Gamma = value;
        }

        public float Alpha
        {
            get => _Alpha;
            set => _Alpha = value;
        }

        public QPolicy Policy => _policy;

        public string CurrentState => State;

        public string CurrentAction => Action;


        public QLearning()
        {
            for (int i = 0; i < _pastReward.Length; i++)
            {
                _pastReward[i] = 0.0f;
            }
        }



        // predicts reinforcement for current round
        //protected abstract double predict(Sensors new_state);

        public void Learn(IQAgent agent, string currentState)
        {
            float reward = 1.0f;
            float newQvalue = 0.0f, res;
            string newState = string.Empty;
            string maxNewState = string.Empty;
            string maxAction = string.Empty;
            string action = string.Empty;

            //float newQvalue = (1.0f - _Alpha) * _Policy.GetQValues(currentState_, action_) + _Alpha * (reward * _Gamma * ValueState(newState_, action_));
            //_Policy.SetQValue(currentState_, action_, newQvalue);

            for (int i = 0; i < _policy.GetNumberOfActions(agent, currentState); i++)
            {
                newState = _policy.GetNewStateFromAction(agent, currentState, i);
                action = _policy.GetActionNumber(agent, currentState, i);

                reward = agent.GetReward(action);

                res = (1.0f - _Alpha) * _policy.GetQValues(currentState, i) + (reward + _Gamma * MaxValueState(agent, /*newState*/currentState));

                if (res > newQvalue)
                {
                    newQvalue = res;
                    maxNewState = newState;
                    maxAction = action;
                }
            }

            //on change les reward ou probabilité ??
            _policy.SetQValue(currentState, maxAction, newQvalue);

            State = maxNewState;
            Action = maxAction;
        }

        float ValueState(string state, string action)
        {
            return _policy.GetQValues(state, action);
        }

        float ValueState(string state, int numAction)
        {
            return _policy.GetQValues(state, numAction);
        }

        float MaxValueState(IQAgent agent, string state)
        {
            float res = 0.0f;
            float r;

            for (int i = 0; i < _policy.GetNumberOfActions(agent, state); i++)
            {
                r = ValueState(state, i);
                if (r > res)
                {
                    res = r;
                }
            }

            return res;
        }

    }
}
