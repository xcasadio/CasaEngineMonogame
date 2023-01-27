#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
	/// <summary>
	/// This class represents the binary inversion mutation operator for chromosomes composed of bits
	/// (bool values). The operator is applied to all genes from a chromosome. If the mutation takes place, then
	/// the gene value is swaped
	/// </summary>
	/// <example>
	/// Chromosome = (0 1 1 0)
	/// There´s a mutation in the 2nd and 3 element (1 1).
	/// New chromosome = (0 0 0 0)
	/// </example>
	public sealed class BinaryInversionMutation : MutationAlgorithm<bool>
	{
		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="probability">The mutation probabilty</param>
		/// <param name="generator">Random number generator</param>
		public BinaryInversionMutation(double probability, Random generator)
			: base(probability, generator) {}

        #endregion

        #region Methods

		/// <summary>
		/// Applies the mutation operator
		/// </summary>
		/// <param name="population">The population we want to mutate</param>
		public override void Mutate(Population<bool> population)
        {
			for (int i = 0; i < population.Genome.Count; i++)
				for (int j = 0; j < population[i].Genotype.Count; j++)
					if (generator.NextDouble() <= this.probability)
						population[i][j] = !population[i][j];
        }

        #endregion
	}
}
