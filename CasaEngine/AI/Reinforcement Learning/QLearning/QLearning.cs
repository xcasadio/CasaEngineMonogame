using System;
using System.Collections.Generic;
using System.Text;

namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
	/// <summary>
	/// 
	/// </summary>
	public class QLearning
	{
		#region Fields

		/// <summary>
		/// reward discount factor (between 0 and 1)
		/// </summary>
		protected float m_Gamma = 0.8f;
		/// <summary>
		/// learning rate (between 0 and 1)
		/// </summary>
		protected float m_Alpha = 0.9f;
		/// <summary>
		/// parameter for the epsilon-greedy policy (between 0 and 1)
		/// </summary>
		protected float m_Epsilon = 0.8f;

		public string m_State = "Idle";
		public string m_Action = "Idling";

		/*protected AgentInitInfo m_Info; //< initialization info
		protected Approximator m_Approximator; //< function approximator we are using
		protected Sensors m_PrevState;                  //< previous state
		protected Actions m_PrevAction;                 //< previous action taken
		protected Actions m_newAction;           //< new action*/

		/// <summary>
		/// 
		/// </summary>
		QPolicy m_Policy = new QPolicy();

		List<KeyValuePair<string, string>> m_PastActions = new List<KeyValuePair<string, string>>();
		float[] m_PastReward = new float[50];
	
		#endregion

		#region Properties

		/// <summary>
		/// Gets/Sets Epsilon
		/// </summary>
		public float Epsilon
		{
			get { return m_Epsilon; }
			set { m_Epsilon = value; }
		}

		/// <summary>
		/// Gets/Sets Gamma
		/// </summary>
		public float Gamma
		{
			get { return m_Gamma; }
			set { m_Gamma = value; }
		}

		/// <summary>
		/// Gets/Sets Alpha
		/// </summary>
		public float Alpha
		{
			get { return m_Alpha; }
			set { m_Alpha = value; }
		}

		/// <summary>
		/// Gets Policy
		/// </summary>
		public QPolicy Policy
		{
			get { return m_Policy; }
		}

		/// <summary>
		/// Gets current state
		/// </summary>
		public string CurrentState
		{
			get { return m_State; }
		}

		/// <summary>
		/// Gets current action
		/// </summary>
		public string CurrentAction
		{
			get { return m_Action; }
		}

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		public QLearning()
		{
			for (int i=0; i<m_PastReward.Length; i++)
			{
				m_PastReward[i] = 0.0f;
			}
		}

		#endregion

		#region Methods

		// predicts reinforcement for current round
		//protected abstract double predict(Sensors new_state);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="agent_"></param>
		/// <param name="currentState_"></param>
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="state_"></param>
		/// <param name="action_"></param>
		/// <returns></returns>
		float ValueState(string state_, string action_)
		{
			return m_Policy.GetQValues(state_, action_);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="state_"></param>
		/// <param name="numAction_"></param>
		/// <returns></returns>
		float ValueState(string state_, int numAction_)
		{
			return m_Policy.GetQValues(state_, numAction_);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="state_"></param>
		/// <returns></returns>
		float MaxValueState(IQAgent agent_, string state_)
		{
			float res = 0.0f;
			float r;

			for (int i=0; i<m_Policy.GetNumberOfActions(agent_, state_); i++)
			{
				r = ValueState(state_, i);
				if (r > res)
				{
					res = r;
				}
			}

			return res;
		}

		#endregion
	}
}
