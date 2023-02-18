namespace CasaEngine.Framework.AI.EvolutionaryComputing.Mutation
{
    public sealed class PermutationInversionMutation : MutationAlgorithm<int>
    {

        public PermutationInversionMutation(double probability, Random generator)
            : base(probability, generator) { }



        public override void Mutate(Population<int> population)
        {
            for (var i = 0; i < population.Genome.Count; i++)
            {
                if (Generator.NextDouble() <= Probability)
                {
                    population[i] = Mutate(population[i]);
                }
            }
        }

        private Chromosome<int> Mutate(Chromosome<int> chromosome)
        {
            int first, second, temp;
            Chromosome<int> finalChromosome;

            //Obtain the exchange indexes
            first = Generator.Next(0, chromosome.Genotype.Count - 1);
            second = Generator.Next(0, chromosome.Genotype.Count - 1);

            while (second == first)
                second = Generator.Next(0, chromosome.Genotype.Count - 1);

            //Order the indexes
            if (first > second)
            {
                temp = first;
                first = second;
                second = temp;
            }

            //Create a new chromosome of the same type of the one passed as parameter
            finalChromosome = (Chromosome<int>)chromosome.Clone();

            //Invert the elements between the indexes
            for (var i = first; i <= second; i++)
            {
                finalChromosome[i] = chromosome[second - i + first];
            }

            return finalChromosome;
        }

    }
}
