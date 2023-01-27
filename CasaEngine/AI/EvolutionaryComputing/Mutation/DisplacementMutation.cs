#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
	/// <summary>
	/// This class represents the displacement mutation operator for chromosomes representing a permutation 
	/// (int valuses).The operator is applied to the chromosome as a whole. If the mutation takes place, then
	/// a chunk of the genome is displaced from his position and the order of the displaced genes is swaped
	/// if the invert flag value is true.
	/// </summary>
	/// <example>
	/// Chromosome = (0 3 1 4 2 5 6)
	/// There´s a mutation. The displacement chunk takes from the 4th to the 6th element (4 2 5).
	/// The chunk is displaced to the 2nd position.
	/// Temp chromosome = (0 4 2 5 3 1 6)
	/// If we have to inver the values, we invert the chunk.
	/// New chromosome = (0 5 2 4 3 1 6)
	/// </example>
	public sealed class DisplacementMutation : MutationAlgorithm<int>
	{
		#region Fields

		/// <summary>
		/// Indicates if we have to invert or not the mutated chunk
		/// </summary>
		internal bool invert;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="probability">The mutation probabilty</param>
		/// <param name="generator">Random number generator</param>
		/// <param name="invert">Invert flag</param>
		public DisplacementMutation(double probability, Random generator, bool invert)
			: base(probability, generator)
		{
			this.invert = invert;
		}

        #endregion

        #region Methods

		/// <summary>
		/// Applies the mutation operator
		/// </summary>
		/// <param name="population">The population we want to mutate</param>
		public override void Mutate(Population<int> population)
		{
			for (int i = 0; i < population.Genome.Count; i++)
				if (this.generator.NextDouble() <= this.probability)
					population[i] = Mutate(population[i]);
		}

		/// <summary>
		/// Auxiliary method that mutates a single chromosome
		/// </summary>
		/// <param name="chromosome">Chromosome we are going to mutate</param>
		/// <returns>The mutated chromosome</returns>
		private Chromosome<int> Mutate(Chromosome<int> chromosome)
        {
            int first, second, temp, position;
			Chromosome<int> finalChromosome;

            //Get the indexes to displace
            first = generator.Next(0, chromosome.Genotype.Count - 1);
			second = generator.Next(0, chromosome.Genotype.Count - 1);
			
			while(second == first)
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

			//Insert the displaced genes in the new chromosome
			if (invert == false)
				for (int i = first; i < second; i++)
					finalChromosome.Genotype.Add(chromosome[i]);

			else
				for (int i = second - 1; i >= first; i--)
					finalChromosome.Genotype.Add(chromosome[i]);

			//Remove those genes from the original chromosome
			chromosome.Genotype.RemoveRange(first, second - first);

			//Select a displacement position
			position = generator.Next(0, chromosome.Genotype.Count - 1);

			//Insert the genes that were before the displacement position in the original gene
			//at the start of the new chromosome
			for (int i = 0; i < position; i++)
				finalChromosome.Genotype.Insert(i, chromosome[i]);

			//Append the genes that were after the displacement position in the original gene
			for (int i = position; i < chromosome.Genotype.Count - 1; i++)
				finalChromosome.Genotype.Add(chromosome[i]);

            return finalChromosome;
        }

        #endregion
	}
}
