namespace CasaEngine.Framework.AI.EvolutionaryComputing.Replacement
{
    public abstract class ReplacementAlgorithm<T>
    {

        protected internal int NewPopulationSize;

        protected internal EvolutionObjective Objective;



        protected ReplacementAlgorithm(int newPopulationSize, EvolutionObjective objective)
        {
            var message = string.Empty;

            if (ValidateNewPopulationSize(newPopulationSize, ref message) == false)
            {
                throw new AiException("newPopulationSize", GetType().ToString(), message);
            }

            NewPopulationSize = newPopulationSize;
            Objective = objective;
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
