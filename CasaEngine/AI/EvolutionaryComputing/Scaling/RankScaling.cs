#region Using Directives

using System;




#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Scaling
{
	/// <summary>
	/// This class represents the rank scaling operator. The new fitness for
	/// each chromosome is based in its position when the whole population is
	/// ordered. More fitness is given to the best ranked chromosomes. The alpha
	/// parammeter indicates how many times the best individual should be choosen
	/// by normal chance.
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public sealed class RankScaling<T> : ScalingAlgorithm<T>
	{
		#region Fields

		/// <summary>
		/// Number of selections of the best individual
		/// </summary>
		internal double alpha;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="objective">Evolution objective</param>
		/// <param name="alpha">Number of selections of the best individual</param>
		public RankScaling(EvolutionObjective objective, double alpha)
			: base(objective)
		{
			String message = String.Empty;

			if (ValidateAlpha(alpha, ref message) == false)
				throw new AIException("alpha", this.GetType().ToString(), message);

			this.alpha = alpha;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Applies the scaling operator
		/// </summary>
		/// <param name="population">The population we want to scale</param>
		/// <returns>The scaled mapping of the population</returns>
		public override ScalingMapping<T> Scale(Population<T> population)
		{
			ScalingMapping<T> mapping;
			double temp;
			int popSize;

			//Create the mapping
			mapping = new ScalingMapping<T>(population.Genome.Count);

			//Order the population on inverse order depending on the objective
			if (objective == EvolutionObjective.Maximize)
				population.Genome.Sort(new ChromosomeComparer<T>(EvolutionObjective.Minimize));

			else
				population.Genome.Sort(new ChromosomeComparer<T>(EvolutionObjective.Maximize));

			//Calculate the new fitness values (ranks) for the mapping
			popSize = population.Genome.Count;
			for (int i = 0; i < popSize; i++)
			{
				temp = ((2.0 - alpha) / popSize) + (2.0 * i * (alpha - 1.0) / (popSize * (popSize - 1.0)));
				mapping.AddChromosome(population[i], temp);
			}

			return mapping;
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the number of selections of the best individual is correct (in the interval (1, 2])
		/// </summary>
		/// <param name="alpha">The number of selections of the best individual value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateAlpha(double alpha, ref string message)
		{
			if (alpha <= 1.0 || alpha > 2.0)
			{
				message = "Alpha value must be in the interval (1, 2]";
				return false;
			}

			return true;
		}

		#endregion
	}
}
