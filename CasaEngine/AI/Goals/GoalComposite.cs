namespace CasaEngine.AI.Goals
{
    [Serializable]
    public abstract class GoalComposite<T> : Goal<T> where T : BaseEntity
    {

        protected internal List<Goal<T>> subGoals;



        public GoalComposite(T owner)
            : base(owner)
        {
            this.subGoals = new List<Goal<T>>();
        }



        public override void Terminate()
        {
            RemoveAllSubgoals();
        }

        public override void AddFrontSubgoal(Goal<T> goal)
        {
            subGoals.Insert(0, goal);
        }

        public override void AddRearSubgoal(Goal<T> goal)
        {
            subGoals.Add(goal);
        }

        public override bool HandleMessage(Message message)
        {
            return ForwardMessage(message);
        }



        protected GoalProcessingState ProcessSubgoals()
        {
            GoalProcessingState subGoalStatus;

            //Remove the Completed or Failed goals from the goals list
            while (subGoals.Count != 0 && (subGoals[0].Status == GoalProcessingState.Completed || subGoals[0].Status == GoalProcessingState.Failed))
            {
                subGoals[0].Terminate();
                subGoals.RemoveAt(0);
            }

            //If there are goals left, process the first one
            if (subGoals.Count != 0)
            {
                //Process the first subgoal
                subGoalStatus = subGoals[0].Process();

                //If the goal was completed, but there are goals left, the composite goal continues to be active
                if (subGoalStatus == GoalProcessingState.Completed && subGoals.Count > 1)
                    return GoalProcessingState.Active;

                //If not, return the status of the goal
                return subGoalStatus;
            }

            //There aren´t any goals left
            return GoalProcessingState.Completed;
        }

        protected void RemoveAllSubgoals()
        {
            for (int i = 0; i < subGoals.Count; i++)
                subGoals[i].Terminate();

            subGoals.Clear();
        }

        protected bool ForwardMessage(Message message)
        {
            if (subGoals.Count != 0)
                return subGoals[0].HandleMessage(message);

            return false;
        }

    }
}
