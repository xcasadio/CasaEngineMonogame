#region Using Directives

using System;
using System.Collections.Generic;




#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Crossover
{
	/// <summary>
	/// Base class for all crossover algorithms. A crossover takes a list of parents and produce a list
	/// of offsprings
	/// </summary>
	/// <typeparam name="T">The type of the genes</typeparam>
	public abstract class CrossoverAlgorithm<T>
	{
		#region Fields

		/// <summary>
		/// The probability of crossover
		/// </summary>
		protected internal double probability;

		/// <summary>
		/// A random number generator
		/// </summary>
		protected internal Random generator;

		#endregion

        #region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="probability">Probability of crossover</param>
		/// <param name="generator">Random number generator</param>
		protected CrossoverAlgorithm(double probability, Random generator)
		{
			String message = String.Empty;

			//Validate values
			if (ValidateProbability(probability, ref message) == false)
				throw new AIException("probality", this.GetType().ToString(), message);

			if (ValidateGenerator(generator, ref message) == false)
				throw new AIException("generator", this.GetType().ToString(), message);

			this.probability = probability;
			this.generator = generator;
		}

        #endregion

        #region Methods

		/// <summary>
		/// Crossover function
		/// </summary>
		/// <remarks>
		/// Returns the parents if the probability indicates a crossover wasn´t generated
		/// </remarks>
		/// <param name="parents">The parents to cross</param>
		/// <returns>The list of offsprings</returns>
		public virtual List<Chromosome<T>> Crossover(List<Chromosome<T>> parents)
		{
			if (generator.NextDouble() > probability)
				return parents;

			return null;
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the probability value is correct (between [0, 1])
		/// </summary>
		/// <param name="probability">The probability value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateProbability(double probability, ref String message)
		{
			if (probability < 0 || probability > 1)
			{
				message = "The probability value must be between 0.0 and 1.0";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Validates if the generator value is correct (not null)
		/// </summary>
		/// <param name="generator">The generator value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateGenerator(Random generator, ref String message)
		{
			if (generator == null)
			{
				message = "The random number generator can´t be null.";
				return false;
			}

			return true;
		}

		#endregion
	}
}
