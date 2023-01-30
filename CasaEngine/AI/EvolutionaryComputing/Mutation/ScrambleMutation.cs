
namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
    public sealed class ScrambleMutation : MutationAlgorithm<int>
    {

        public ScrambleMutation(double probability, Random generator)
            : base(probability, generator) { }



        public override void Mutate(Population<int> population)
        {
            for (int i = 0; i < population.Genome.Count; i++)
                if (generator.NextDouble() <= this.probability)
                    population[i] = Mutate(population[i]);
        }

        private Chromosome<int> Mutate(Chromosome<int> chromosome)
        {
            int first, second, temp;
            Chromosome<int> finalChromosome;

            //Get the indexes to scramble
            first = generator.Next(0, chromosome.Genotype.Count - 1);
            second = generator.Next(0, chromosome.Genotype.Count - 1);

            while (second == first)
                second = generator.Next(0, chromosome.Genotype.Count - 1);

            //Order the indexes
            if (first > second)
            {
                temp = first;
                first = second;
                second = temp;
            }

            //Create a new chromosome of the same type of the one used as parameter
            finalChromosome = chromosome.FastEmptyInstance();

            //Copy the first genes into the new chromosome
            for (int i = 0; i < first; i++)
                finalChromosome.Genotype.Add(chromosome[i]);

            chromosome.Genotype.RemoveRange(0, first);

            //Scramble the selected genes
            for (int i = 0; i < second - first; i++)
            {
                temp = generator.Next(0, second - first - i);
                finalChromosome.Genotype.Add(chromosome[temp]);
                chromosome.Genotype.RemoveAt(temp);
            }

            //Add the last genes
            finalChromosome.Genotype.AddRange(chromosome.Genotype);

            return finalChromosome;
        }

    }
}
