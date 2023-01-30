namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
    public class QLearning
    {

        protected float m_Gamma = 0.8f;
        protected float m_Alpha = 0.9f;
        protected float m_Epsilon = 0.8f;

        public string m_State = "Idle";
        public string m_Action = "Idling";

        /*protected AgentInitInfo m_Info; //< initialization info
		protected Approximator m_Approximator; //< function approximator we are using
		protected Sensors m_PrevState;                  //< previous state
		protected Actions m_PrevAction;                 //< previous action taken
		protected Actions m_newAction;           //< new action*/

        readonly QPolicy m_Policy = new QPolicy();

        List<KeyValuePair<string, string>> m_PastActions = new List<KeyValuePair<string, string>>();
        readonly float[] m_PastReward = new float[50];



        public float Epsilon
        {
            get => m_Epsilon;
            set => m_Epsilon = value;
        }

        public float Gamma
        {
            get => m_Gamma;
            set => m_Gamma = value;
        }

        public float Alpha
        {
            get => m_Alpha;
            set => m_Alpha = value;
        }

        public QPolicy Policy => m_Policy;

        public string CurrentState => m_State;

        public string CurrentAction => m_Action;


        public QLearning()
        {
            for (int i = 0; i < m_PastReward.Length; i++)
            {
                m_PastReward[i] = 0.0f;
            }
        }



        // predicts reinforcement for current round
        //protected abstract double predict(Sensors new_state);

        public void Learn(IQAgent agent_, string currentState_)
        {
            float reward = 1.0f;
            float newQvalue = 0.0f, res;
            string newState = string.Empty;
            string maxNewState = string.Empty;
            string maxAction = string.Empty;
            string action = string.Empty;

            //float newQvalue = (1.0f - m_Alpha) * m_Policy.GetQValues(currentState_, action_) + m_Alpha * (reward * m_Gamma * ValueState(newState_, action_));
            //m_Policy.SetQValue(currentState_, action_, newQvalue);

            for (int i = 0; i < m_Policy.GetNumberOfActions(agent_, currentState_); i++)
            {
                newState = m_Policy.GetNewStateFromAction(agent_, currentState_, i);
                action = m_Policy.GetActionNumber(agent_, currentState_, i);

                reward = agent_.GetReward(action);

                res = (1.0f - m_Alpha) * m_Policy.GetQValues(currentState_, i) + (reward + m_Gamma * MaxValueState(agent_, /*newState*/currentState_));

                if (res > newQvalue)
                {
                    newQvalue = res;
                    maxNewState = newState;
                    maxAction = action;
                }
            }

            //on change les reward ou probabilité ??
            m_Policy.SetQValue(currentState_, maxAction, newQvalue);

            m_State = maxNewState;
            m_Action = maxAction;
        }

        float ValueState(string state_, string action_)
        {
            return m_Policy.GetQValues(state_, action_);
        }

        float ValueState(string state_, int numAction_)
        {
            return m_Policy.GetQValues(state_, numAction_);
        }

        float MaxValueState(IQAgent agent_, string state_)
        {
            float res = 0.0f;
            float r;

            for (int i = 0; i < m_Policy.GetNumberOfActions(agent_, state_); i++)
            {
                r = ValueState(state_, i);
                if (r > res)
                {
                    res = r;
                }
            }

            return res;
        }

    }
}
