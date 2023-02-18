namespace CasaEngine.Framework.AI.EvolutionaryComputing.Scaling
{
    public abstract class ScalingAlgorithm<T>
    {

        protected internal EvolutionObjective Objective;



        protected ScalingAlgorithm(EvolutionObjective objective)
        {
            Objective = objective;
        }



        public abstract ScalingMapping<T> Scale(Population<T> population);

    }
}
