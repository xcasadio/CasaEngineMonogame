using Microsoft.Xna.Framework;
using CasaEngineCommon.Helper;

//using Mathematics;


namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
    public class SteeringBehaviors
    {

        protected internal MovingEntity Owner;

        protected internal List<SteeringBehavior> Behaviors;

        protected internal SumMethod SumAlgorithm;



        public SteeringBehaviors(MovingEntity owner, SumMethod sumAlgorithm)
        {
            this.Owner = owner;
            this.sumAlgorithm = sumAlgorithm;

            Behaviors = new List<SteeringBehavior>();
        }



        public SumMethod SumAlgorithm
        {
            get => sumAlgorithm;
            set => sumAlgorithm = value;
        }



        public void RegisterBehavior<T>(String name, float modifier)
        {
            Object newBehavior;
            Object[] parametters;

            foreach (SteeringBehavior behavior in Behaviors)
                if (behavior is T && behavior.Name == name)
                    return;

            parametters = new object[3];
            parametters[0] = name;
            parametters[1] = this.Owner;
            parametters[2] = modifier;

            newBehavior = Activator.CreateInstance(typeof(T), parametters);

            Behaviors.Add((SteeringBehavior)newBehavior);
        }

        public void RegisterBehavior(SteeringBehavior behavior)
        {
            for (int i = 0; i < Behaviors.Count; i++)
                if (Behaviors[i].GetType() == behavior.GetType() && Behaviors[i].Name == behavior.Name)
                {
                    Behaviors[i] = behavior;
                    return;
                }

            Behaviors.Add(behavior);
        }

        public void UnregisterBehavior<T>(String name)
        {
            for (int i = 0; i < Behaviors.Count; i++)
                if (Behaviors[i] is T && Behaviors[i].Name == name)
                    Behaviors.RemoveAt(i);
        }

        public void ActivateBehavior<T>(String name)
        {
            foreach (SteeringBehavior behavior in Behaviors)
                if (behavior is T && behavior.Name == name)
                    behavior.Active = true;
        }

        public void DeactivateBehavior<T>(String name)
        {
            foreach (SteeringBehavior behavior in Behaviors)
                if (behavior is T && behavior.Name == name)
                    behavior.Active = false;
        }

        public T GetBehavior<T>(String name) where T : SteeringBehavior
        {
            foreach (SteeringBehavior behavior in Behaviors)
                if (behavior is T && behavior.Name == name)
                    return (T)behavior;

            return null;
        }

        public Vector3 Calculate()
        {
            return Vector3Helper.Truncate(sumAlgorithm(Behaviors), Owner.MaxForce);
        }

    }
}
