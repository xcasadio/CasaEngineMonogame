using System;
using System.Collections.Generic;
using System.Text;

namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
	/// <summary>
	/// 
	/// </summary>
	public interface IQAgent
	{
		float GetReward(string actionToDo_);
		bool IsActionIsPossible(string action_);
	}
}
