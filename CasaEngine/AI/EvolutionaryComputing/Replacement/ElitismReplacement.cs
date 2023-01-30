
namespace CasaEngine.AI.EvolutionaryComputing.Replacement
{
    public sealed class ElitismReplacement<T> : ReplacementAlgorithm<T>
    {

        internal int numberParents;



        public ElitismReplacement(int newPopulationSize, EvolutionObjective objective, int numberParents)
            : base(newPopulationSize, objective)
        {
            String message = String.Empty;

            //Validate arguments
            if (ValidateNumberParents(newPopulationSize, numberParents, ref message) == false)
                throw new AIException("newPopulationSize", this.GetType().ToString(), message);

            this.numberParents = numberParents;
        }



        public override Population<T> Replace(Population<T> parents, Population<T> children)
        {
            Population<T> survivors;

            //Sort parents and children
            parents.Genome.Sort(new ChromosomeComparer<T>(objective));
            children.Genome.Sort(new ChromosomeComparer<T>(objective));

            //Create the survivors population
            survivors = parents.FastEmptyInstance();

            //Copy the elite parents
            survivors.Genome.AddRange(parents.Genome.GetRange(0, numberParents));

            //Copy the best children
            survivors.Genome.AddRange(children.Genome.GetRange(0, newPopulationSize - numberParents));

            return survivors;
        }



        public static bool ValidateNumberParents(int newPopulationSize, int numberParents, ref string message)
        {
            if (numberParents < 0)
            {
                message = "Number of selected parents must be at least 0.";
                return false;
            }

            if (numberParents >= newPopulationSize)
            {
                message = "Number of selected parents must be lower than the new population size.";
                return false;
            }

            return true;
        }

    }
}
