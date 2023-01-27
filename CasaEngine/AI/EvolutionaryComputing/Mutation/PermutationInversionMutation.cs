#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
	/// <summary>
	/// This class represents the inversion mutation operator for chromosomes representing a permutation 
	/// (int values).The operator is applied to the chromosome as a whole. If the mutation takes place, then
	/// a chunk of the genome order is swaped.
	/// </summary>
	/// <example>
	/// Chromosome = (0 3 1 4 2 5 6)
	/// There´s a mutation. The inversion chunk takes from the 2th to the 3th element (3 1).
	/// New chromosome = (0 1 3 4 2 5 6)
	/// </example>
	public sealed class PermutationInversionMutation : MutationAlgorithm<int>
	{
		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="probability">The mutation probabilty</param>
		/// <param name="generator">Random number generator</param>
		public PermutationInversionMutation(double probability, Random generator)
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
            int first, second, temp;
			Chromosome<int> finalChromosome;
            
			//Obtain the exchange indexes
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

			//Create a new chromosome of the same type of the one passed as parameter
			finalChromosome = (Chromosome<int>) chromosome.Clone();

			//Invert the elements between the indexes
            for (int i = first; i <= second; i++)
                finalChromosome[i] = chromosome[second - i + first];

            return finalChromosome;
        }

        #endregion
	}
}
