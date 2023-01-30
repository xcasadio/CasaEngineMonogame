
namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
    public sealed class ExchangeMutation : MutationAlgorithm<int>
    {

        public ExchangeMutation(double probability, Random generator)
            : base(probability, generator) { }



        public override void Mutate(Population<int> population)
        {
            for (int i = 0; i < population.Genome.Count; i++)
                if (this.generator.NextDouble() <= this.probability)
                    population[i] = Mutate(population[i]);
        }

        private Chromosome<int> Mutate(Chromosome<int> chromosome)
        {
            int first, second, temp;

            //Get the indexes to exchange
            first = generator.Next(0, chromosome.Genotype.Count - 1);
            second = generator.Next(0, chromosome.Genotype.Count - 1);

            while (second == first)
                second = generator.Next(0, chromosome.Genotype.Count - 1);

            //Swap the selected indexes
            temp = chromosome[first];
            chromosome[first] = chromosome[second];
            chromosome[second] = temp;

            return chromosome;
        }

    }
}
