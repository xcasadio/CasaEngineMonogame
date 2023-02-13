
namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
    public sealed class BinaryInversionMutation : MutationAlgorithm<bool>
    {

        public BinaryInversionMutation(double probability, Random generator)
            : base(probability, generator) { }



        public override void Mutate(Population<bool> population)
        {
            for (var i = 0; i < population.Genome.Count; i++)
            {
                for (var j = 0; j < population[i].Genotype.Count; j++)
                {
                    if (Generator.NextDouble() <= Probability)
                    {
                        population[i][j] = !population[i][j];
                    }
                }
            }
        }

    }
}
