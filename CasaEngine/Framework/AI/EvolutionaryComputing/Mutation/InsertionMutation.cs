namespace CasaEngine.Framework.AI.EvolutionaryComputing.Mutation;

public sealed class InsertionMutation : MutationAlgorithm<int>
{

    public InsertionMutation(double probability, Random generator)
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
        int selectedGene, position, gene;

        //Choose the gene to move
        selectedGene = Generator.Next(0, chromosome.Genotype.Count - 1);

        //Choose the position to move it
        position = Generator.Next(0, chromosome.Genotype.Count - 1);

        //IsRemoved the selected gene
        gene = chromosome.Genotype[selectedGene];
        chromosome.Genotype.RemoveAt(selectedGene);

        //Insert the gene
        if (position < chromosome.Genotype.Count)
        {
            chromosome.Genotype.Insert(position, gene);
        }

        else
        {
            chromosome.Genotype.Add(gene);
        }

        return chromosome;
    }

}