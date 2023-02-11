
namespace CasaEngine.AI.EvolutionaryComputing.Replacement
{
    public sealed class ChildrenReplacement<T> : ReplacementAlgorithm<T>
    {

        public ChildrenReplacement(int newPopulationSize, EvolutionObjective objective)
            : base(newPopulationSize, objective) { }



        public override Population<T> Replace(Population<T> parents, Population<T> children)
        {
            Population<T> survivors;

            //Check the population sizes
            if (NewPopulationSize > children.Genome.Count)
                throw new Exception("The new population size can´t be greater than the number of children.");

            //Sort children
            children.Genome.Sort(new ChromosomeComparer<T>(Objective));

            //Copy the best children to the new population
            survivors = parents.FastEmptyInstance();
            survivors.Genome.AddRange(children.Genome.GetRange(0, NewPopulationSize));

            return survivors;
        }

    }
}
