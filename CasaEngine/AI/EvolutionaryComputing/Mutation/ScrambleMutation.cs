#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
	/// <summary>
	/// This class represents the scramble mutation operator for chromosomes representing a permutation 
	/// (int values).The operator is applied to the chromosome as a whole. If the mutation takes place, then
	/// 1 random gene is selected and inserted in another position of the chromosome.
	/// </summary>
	/// <example>
	/// Chromosome = (0 3 1 4 2 5 6)
	/// There´s a mutation. The index of selected gene is 2 (1) and the insertion position is 5. We insert the gene
	/// New chromosome = (0	3 4 2 1 5 6)
	/// </example>
	public sealed class ScrambleMutation : MutationAlgorithm<int>
	{
		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="probability">The mutation probabilty</param>
		/// <param name="generator">Random number generator</param>
		public ScrambleMutation(double probability, Random generator)
			: base(probability, generator) {}

        #endregion

		#region Methods

		/// <summary>
		/// Applies the mutation operator
		/// </summary>
		/// <param name="population">The population we want to mutate</param>
		public override void Mutate(Population<int> population)
        {
            for (int i = 0; i < population.Genome.Count; i++)
				if (generator.NextDouble() <= this.probability)
                    population[i] = Mutate(population[i]);
        }

		/// <summary>
		/// Auxiliary method that mutates a single chromosome
		/// </summary>
		/// <param name="chromosome">Chromosome we are going to mutate</param>
		/// <returns>The mutated chromosome</returns>
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

		#endregion
	}
}
