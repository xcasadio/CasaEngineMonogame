#region Using Directives

using System;




#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
	/// <summary>
	/// This class represents the aleatory mutation operator for chromosomes composed of ints.
	/// The operator is applied to all genes from a chromosome. If the mutation takes place, then
	/// the gene is replaced by a random number between two limits.
	/// </summary>
	/// <example>
	/// Chromosome = (0 2 1 5), Ceil = 0, Floor = 10
	/// There´s a mutation in the 2nd element (2), the random number between [0, 10] is 7.
	/// New chromosome = (0 7 1 5)
	/// </example>
	public sealed class AleatoryMutation : MutationAlgorithm<int>
	{
		#region Fields

		/// <summary>
		/// Lower limit of the mutation range
		/// </summary>
		internal int floor;

		/// <summary>
		/// Upper limit of the mutation range
		/// </summary>
		internal int ceil;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="probability">The mutation probabilty</param>
		/// <param name="generator">Random number generator</param>
		/// <param name="floor">Lower limit of the range</param>
		/// <param name="ceil">Upper limit of the range</param>
		public AleatoryMutation(double probability, Random generator, int floor, int ceil)
			: base(probability, generator)
		{
			String message = String.Empty;

			//Validate params
			if (ValidateLimits(floor, ceil, ref message) == false)
				throw new AIException("floor-ceil", this.GetType().ToString(), message);

			this.floor = floor;
			this.ceil = ceil;
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
				for (int j = 0; j < population[i].Genotype.Count; j++)
					if (generator.NextDouble() <= this.probability)
						population[i].Genotype[j] = generator.Next(this.floor, this.ceil);
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if a floor and a ceil value are correct (floor must be lower than ceil)
		/// </summary>
		/// <param name="floor">Lower limit to validate</param>
		/// <param name="ceil">Higher limit to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the values are correct. False if they are not</returns>
		public static bool ValidateLimits(int floor, int ceil, ref String message)
		{
			if (floor > ceil)
			{
				message = "The floor value must be lower than the ceil value.";
				return false;
			}

			return true;
		}

		#endregion
	}
}
