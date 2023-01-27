#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
	/// <summary>
	/// This class represents the exchange mutation operator for chromosomes representing a permutation 
	/// (int values).The operator is applied to the chromosome as a whole. If the mutation takes place, then
	/// 2 random genes are selected and their values exchanged.
	/// </summary>
	/// <example>
	/// Chromosome = (6 3 1 4 2 5 0)
	/// There´s a mutation. The selected genes are index 0 (6) and 6 (0). This genes are exchanged.
	/// New chromosome = (0 4 2 5 3 1 6)
	/// </example>
	public sealed class ExchangeMutation : MutationAlgorithm<int>
	{
		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="probability">The mutation probabilty</param>
		/// <param name="generator">Random number generator</param>
		public ExchangeMutation(double probability, Random generator)
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

            //Get the indexes to exchange
            first = generator.Next(0, chromosome.Genotype.Count - 1);
			second = generator.Next(0, chromosome.Genotype.Count - 1);

            while(second == first)
				second = generator.Next(0, chromosome.Genotype.Count - 1);

            //Swap the selected indexes
            temp = chromosome[first];
            chromosome[first] = chromosome[second];
            chromosome[second] = temp;

            return chromosome;
        }

        #endregion
	}
}
