namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
    public interface IQAgent
    {
        float GetReward(string actionToDo);
        bool IsActionIsPossible(string action);
    }
}
