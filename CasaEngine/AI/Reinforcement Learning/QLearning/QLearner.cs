using System;
using System.Collections.Generic;
using System.Text;
using CasaEngine.Gameplay.Actor.Object;


namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
	/// <summary>
	/// 
	/// </summary>
	public class QLearner
		: BaseObject
	{
		#region Fields

		QLearning m_QL = new QLearning();
		
		#endregion

		#region Properties

		/// <summary>
		/// Gets QLearning
		/// </summary>
		public QLearning QLearning
		{
			get { return m_QL; }
		}

		#endregion

		#region Constructors

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dt"></param>
		public void Update(float dt)
		{
			
			//string newState = string.Empty;
			//m_QL.Learn(state_, action_, newState);
			//m_QL.Learn()
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="agent_"></param>
		/// <param name="currentState_"></param>
		public void Update(IQAgent agent_, string currentState_)
		{
			m_QL.Learn(agent_, currentState_);
		}

#if EDITOR

        public override bool CompareTo(BaseObject other_)
        {
            throw new Exception("The method or operation is not implemented.");
        }

#endif

        public override BaseObject Clone()
        {
            throw new Exception("The method or operation is not implemented.");
        }
		#endregion
	}
}
