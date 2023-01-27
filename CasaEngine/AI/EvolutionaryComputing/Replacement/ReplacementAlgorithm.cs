#region Using Directives

using System;




#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Replacement
{
	/// <summary>
	/// This abstract class represents a replacement algorithm that can be applied in a
	/// genetic algorithm or evolutionary strategy. This class must provide the means
	/// of creating a new population based in the old parents and their children.
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public abstract class ReplacementAlgorithm<T>
	{
		#region Fields

		/// <summary>
		/// Size of the new population the the operator will generate
		/// </summary>
		protected internal int newPopulationSize;

		/// <summary>
		/// Evolutionary objective to choose the new population
		/// </summary>
		protected internal EvolutionObjective objective;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="newPopulationSize">The size of the new population the replacement will generate</param>
		/// <param name="objective">The objective of the evolution</param>
		protected ReplacementAlgorithm(int newPopulationSize, EvolutionObjective objective)
		{
			String message = String.Empty;

			if (ValidateNewPopulationSize(newPopulationSize, ref message) == false)
				throw new AIException("newPopulationSize", this.GetType().ToString(), message);

			this.newPopulationSize = newPopulationSize;
			this.objective = objective;
		}

        #endregion

        #region Methods

		/// <summary>
		/// Applies the replacement operator
		/// </summary>
		/// <param name="parents">The parents population</param>
		/// <param name="children">The children population</param>
		/// <returns>The new population that will replace the parents</returns>
        public abstract Population<T> Replace(Population<T> parents, Population<T> children);

        #endregion

		#region Validators

		/// <summary>
		/// Validates if the new population size value is correct (greater than 1)
		/// </summary>
		/// <param name="newPopulationSize">New population size value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateNewPopulationSize(int newPopulationSize, ref string message)
		{
			if (newPopulationSize < 2)
			{
				message = "The new population size must be at least 2.";
				return false;
			}

			return true;
		}

		#endregion
	}
}
