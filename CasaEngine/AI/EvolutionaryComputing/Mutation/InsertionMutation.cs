#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
	/// <summary>
	/// This class represents the insertion mutation operator for chromosomes representing a permutation 
	/// (int values).The operator is applied to the chromosome as a whole. If the mutation takes place, then
	/// 1 random gene is selected and inserted in another position of the chromosome.
	/// </summary>
	/// <example>
	/// Chromosome = (0 3 1 4 2 5 6)
	/// There´s a mutation. The index of selected gene is 2 (1) and the insertion position is 5. We insert the gene
	/// New chromosome = (0	3 4 2 1 5 6)
	/// </example>
	public sealed class InsertionMutation : MutationAlgorithm<int>
	{
		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="probability">The mutation probabilty</param>
		/// <param name="generator">Random number generator</param>
		public InsertionMutation(double probability, Random generator)
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
            int selectedGene, position, gene;

			//Choose the gene to move
			selectedGene = generator.Next(0, chromosome.Genotype.Count - 1);

			//Choose the position to move it
			position = generator.Next(0, chromosome.Genotype.Count - 1);

			//Remove the selected gene
			gene = chromosome.Genotype[selectedGene];
			chromosome.Genotype.RemoveAt(selectedGene);

			//Insert the gene
			if (position < chromosome.Genotype.Count)
				chromosome.Genotype.Insert(position, gene);

			else
				chromosome.Genotype.Add(gene);

            return chromosome;
        }

        #endregion
	}
}
