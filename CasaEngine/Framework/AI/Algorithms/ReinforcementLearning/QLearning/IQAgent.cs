namespace CasaEngine.Framework.AI.Algorithms.ReinforcementLearning.QLearning;

public interface IQAgent
{
    float GetReward(string actionToDo);
    bool IsActionIsPossible(string action);
}