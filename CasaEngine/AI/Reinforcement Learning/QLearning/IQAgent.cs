namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
    public interface IQAgent
    {
        float GetReward(string actionToDo_);
        bool IsActionIsPossible(string action_);
    }
}
