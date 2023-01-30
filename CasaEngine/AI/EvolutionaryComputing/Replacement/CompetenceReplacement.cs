
namespace CasaEngine.AI.EvolutionaryComputing.Replacement
{
    public sealed class CompetenceReplacement<T> : ReplacementAlgorithm<T>
    {

        public CompetenceReplacement(int newPopulationSize, EvolutionObjective objective)
            : base(newPopulationSize, objective) { }



        public override Population<T> Replace(Population<T> parents, Population<T> children)
        {
            Population<T> survivors;

            // Mix the parents and the children to create a new population
            survivors = parents.FastEmptyInstance();
            survivors.Genome.AddRange(parents.Genome);
            survivors.Genome.AddRange(children.Genome);

            //Sort the survivors
            survivors.Genome.Sort(new ChromosomeComparer<T>(objective));

            // Remove the less fit chromosomes
            survivors.Genome.RemoveRange(newPopulationSize, survivors.Genome.Count - newPopulationSize);

            return survivors;
        }

    }
}
