
namespace CasaEngine.AI.EvolutionaryComputing.Replacement
{
    public abstract class ReplacementAlgorithm<T>
    {

        protected internal int newPopulationSize;

        protected internal EvolutionObjective objective;



        protected ReplacementAlgorithm(int newPopulationSize, EvolutionObjective objective)
        {
            String message = String.Empty;

            if (ValidateNewPopulationSize(newPopulationSize, ref message) == false)
                throw new AIException("newPopulationSize", this.GetType().ToString(), message);

            this.newPopulationSize = newPopulationSize;
            this.objective = objective;
        }



        public abstract Population<T> Replace(Population<T> parents, Population<T> children);



        public static bool ValidateNewPopulationSize(int newPopulationSize, ref string message)
        {
            if (newPopulationSize < 2)
            {
                message = "The new population size must be at least 2.";
                return false;
            }

            return true;
        }

    }
}
