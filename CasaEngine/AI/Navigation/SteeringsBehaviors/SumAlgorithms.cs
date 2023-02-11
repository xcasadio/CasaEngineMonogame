using Microsoft.Xna.Framework;




namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
    public static class SumAlgorithms
    {

        public static Vector3 WeightedSum(List<SteeringBehavior> behaviors)
        {
            Vector3 totalForce;

            totalForce = Vector3.Zero;

            foreach (SteeringBehavior behavior in behaviors)
                if (behavior.Active == true)
                {
                    totalForce += behavior.Calculate() * behavior.Modifier;
                }

            return totalForce;
        }

        public static Vector3 WeightedRunningSumWithPrioritization(List<SteeringBehavior> behaviors)
        {
            Vector3 totalForce;

            totalForce = Vector3.Zero;

            return totalForce;
        }

        public static Vector3 PrioritizedDithering(List<SteeringBehavior> behaviors)
        {
            Vector3 totalForce;

            totalForce = Vector3.Zero;

            return totalForce;
        }

    }
}
