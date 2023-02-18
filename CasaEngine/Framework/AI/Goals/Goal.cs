using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.AI.Goals
{
    [Serializable]
    public abstract class Goal<T> : IMessageable where T : Entity
    {
        protected internal T Owner;
        protected internal GoalProcessingState status;

        public Goal(T owner)
        {
            Owner = owner;
            status = GoalProcessingState.Inactive;
        }

        public GoalProcessingState Status => status;

        public abstract void Activate();

        public abstract GoalProcessingState Process();

        public abstract void Terminate();

        public virtual void AddFrontSubgoal(Goal<T> goal)
        {
            throw new InvalidOperationException("Can´t insert a subgoal in an atomic goal");
        }

        public virtual void AddRearSubgoal(Goal<T> goal)
        {
            throw new InvalidOperationException("Can´t queue a subgoal in an atomic goal");
        }

        public virtual bool HandleMessage(Message message)
        {
            return false;
        }

        protected void ActivateIfInactive()
        {
            if (status == GoalProcessingState.Inactive)
            {
                Activate();
            }
        }

        protected void ReactivateIfFailed()
        {
            if (status == GoalProcessingState.Failed)
            {
                status = GoalProcessingState.Inactive;
            }
        }
    }
}
