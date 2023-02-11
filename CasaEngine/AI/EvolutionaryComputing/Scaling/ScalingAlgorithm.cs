
namespace CasaEngine.AI.EvolutionaryComputing.Scaling
{
    public abstract class ScalingAlgorithm<T>
    {

        protected internal EvolutionObjective Objective;



        protected ScalingAlgorithm(EvolutionObjective objective)
        {
            this.Objective = objective;
        }



        public abstract ScalingMapping<T> Scale(Population<T> population);

    }
}
