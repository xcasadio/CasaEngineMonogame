
namespace CasaEngine.AI.EvolutionaryComputing.Scaling
{
    public abstract class ScalingAlgorithm<T>
    {

        protected internal EvolutionObjective objective;



        protected ScalingAlgorithm(EvolutionObjective objective)
        {
            this.objective = objective;
        }



        public abstract ScalingMapping<T> Scale(Population<T> population);

    }
}
