using CasaEngine.Framework.SceneManagement;

namespace CasaEngine.Framework.AI.Goals;

[Serializable]
public abstract class GoalComposite<T> : Goal<T> where T : AActor
{

    protected internal List<Goal<T>> SubGoals;

    public GoalComposite(T owner)
        : base(owner)
    {
        SubGoals = new List<Goal<T>>();
    }

    public override void Terminate()
    {
        RemoveAllSubgoals();
    }

    public override void AddFrontSubgoal(Goal<T> goal)
    {
        SubGoals.Insert(0, goal);
    }

    public override void AddRearSubgoal(Goal<T> goal)
    {
        SubGoals.Add(goal);
    }

    public override bool HandleMessage(Message message)
    {
        return ForwardMessage(message);
    }

    protected GoalProcessingState ProcessSubgoals()
    {
        GoalProcessingState subGoalStatus;

        //IsRemoved the Completed or Failed goals from the goals list
        while (SubGoals.Count != 0 && (SubGoals[0].Status == GoalProcessingState.Completed || SubGoals[0].Status == GoalProcessingState.Failed))
        {
            SubGoals[0].Terminate();
            SubGoals.RemoveAt(0);
        }

        //If there are goals left, process the first one
        if (SubGoals.Count != 0)
        {
            //Process the first subgoal
            subGoalStatus = SubGoals[0].Process();

            //If the goal was completed, but there are goals left, the composite goal continues to be active
            if (subGoalStatus == GoalProcessingState.Completed && SubGoals.Count > 1)
            {
                return GoalProcessingState.Active;
            }

            //If not, return the status of the goal
            return subGoalStatus;
        }

        //There aren´t any goals left
        return GoalProcessingState.Completed;
    }

    protected void RemoveAllSubgoals()
    {
        for (var i = 0; i < SubGoals.Count; i++)
        {
            SubGoals[i].Terminate();
        }

        SubGoals.Clear();
    }

    protected bool ForwardMessage(Message message)
    {
        if (SubGoals.Count != 0)
        {
            return SubGoals[0].HandleMessage(message);
        }

        return false;
    }

}