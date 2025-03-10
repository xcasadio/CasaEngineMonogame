namespace CasaEngine.Framework.AI.EvolutionaryComputing.Mutation;

public sealed class DisplacementMutation : MutationAlgorithm<int>
{

    internal bool Invert;



    public DisplacementMutation(double probability, Random generator, bool invert)
        : base(probability, generator)
    {
        Invert = invert;
    }



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
        int first, second, temp, position;
        Chromosome<int> finalChromosome;

        //Get the indexes to displace
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

        //Create a new chromosome of the same type of the one used as parameter
        finalChromosome = chromosome.FastEmptyInstance();

        //Insert the displaced genes in the new chromosome
        if (Invert == false)
        {
            for (var i = first; i < second; i++)
            {
                finalChromosome.Genotype.Add(chromosome[i]);
            }
        }

        else
        {
            for (var i = second - 1; i >= first; i--)
            {
                finalChromosome.Genotype.Add(chromosome[i]);
            }
        }

        //IsRemoved those genes from the original chromosome
        chromosome.Genotype.RemoveRange(first, second - first);

        //Select a displacement position
        position = Generator.Next(0, chromosome.Genotype.Count - 1);

        //Insert the genes that were before the displacement position in the original gene
        //at the start of the new chromosome
        for (var i = 0; i < position; i++)
        {
            finalChromosome.Genotype.Insert(i, chromosome[i]);
        }

        //Append the genes that were after the displacement position in the original gene
        for (var i = position; i < chromosome.Genotype.Count - 1; i++)
        {
            finalChromosome.Genotype.Add(chromosome[i]);
        }

        return finalChromosome;
    }

}