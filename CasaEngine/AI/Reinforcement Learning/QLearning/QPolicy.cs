using System;
using System.Collections.Generic;
using System.Text;

namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
	/// <summary>
	/// 
	/// </summary>
	public class QPolicy
	{
		#region Fields

		Dictionary<KeyValuePair<string, string>, float> m_QValues = new Dictionary<KeyValuePair<string, string>, float>();

		#endregion

		#region Properties

		#endregion

		#region Constructors

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="state_"></param>
		/// <param name="action_"></param>
		/// <returns></returns>
		public float GetQValues(string state_, string action_)
		{
			return m_QValues[new KeyValuePair<string, string>(state_, action_)];
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="state_"></param>
		/// <param name="numAction_"></param>
		/// <returns></returns>
		public float GetQValues(string state_, int numAction_)
		{
			int action = 0;

			foreach (KeyValuePair<KeyValuePair<string, string>, float> pair in m_QValues)
			{
				if ( pair.Key.Key.Equals(state_) == true )
				{
					//if (pair.Value != 0.0f) //check if action is possible
					if (action == numAction_)
					{
						return pair.Value;
					}

					action++;
				}
			}

			throw new ArgumentException("GetQValues() : numAction_ is too high");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="state_"></param>
		/// <param name="action_"></param>
		/// <param name="value_"></param>
		public void SetQValue(string state_, string action_, float value_)
		{
			m_QValues[new KeyValuePair<string, string>(state_, action_)] = value_;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="state_"></param>
		/// <returns></returns>
		public int GetNumberOfActions(IQAgent agent_, string state_)
		{
			int action = 0;

			foreach (KeyValuePair<KeyValuePair<string, string>, float> pair in m_QValues)
			{
				if (pair.Key.Key.Equals(state_) == true && agent_.IsActionIsPossible(pair.Key.Value))
				{
					//if (pair.Value != 0.0f) //check if action is possible
					action++;
				}
			}

			return action;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="?"></param>
		/// <param name="?"></param>
		/// <returns></returns>
		public string GetNewStateFromAction(IQAgent agent_, string state_, int numAction_)
		{
			int action = 0;

			foreach (KeyValuePair<KeyValuePair<string, string>, float> pair in m_QValues)
			{
				if (pair.Key.Key.Equals(state_) == true && agent_.IsActionIsPossible(pair.Key.Value))
				{
					if (action == numAction_)
					{
						return pair.Key.Key;
					}

					//if (pair.Value != 0.0f) //check if action is possible
					action++;
				}
			}

			throw new ArgumentException("GetNewStateFromAction() : numAction_ is too high");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="state_"></param>
		/// <param name="numAction_"></param>
		/// <returns></returns>
		public string GetActionNumber(IQAgent agent_, string state_, int numAction_)
		{
			int action = 0;

			foreach (KeyValuePair<KeyValuePair<string, string>, float> pair in m_QValues)
			{
				if (pair.Key.Key.Equals(state_) == true && agent_.IsActionIsPossible(pair.Key.Value))
				{
					if (action == numAction_)
					{
						return pair.Key.Value;
					}

					//if (pair.Value != 0.0f) //check if action is possible
					action++;
				}
			}

			throw new ArgumentException("GetNewStateFromAction() : numAction_ is too high");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="states_"></param>
		/// <param name="values_"></param>
		public void CreateStatesAndActions(KeyValuePair<string, string>[] states_, float[] values_)
		{
			if (states_.Length != values_.Length)
			{
				throw new ArgumentException("CreateStatesAndActions() : states_.Length != values_.Length");
			}

			for (int i = 0; i < states_.Length; i++)
			{
				m_QValues.Add(states_[i], values_[i]);
			}
		}	

		#endregion
	}
}
