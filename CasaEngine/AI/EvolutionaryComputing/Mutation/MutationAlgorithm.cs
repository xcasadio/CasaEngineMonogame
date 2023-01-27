#region Using Directives

using System;




#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
	/// <summary>
	/// This abstract class represents a mutation algorithm that can be applied in a
	/// genetic algorithm or evolutionary strategy. This class must provide the means
	/// of mutating a whole population of chromosomes.
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public abstract class MutationAlgorithm<T>
	{
		#region Fields

		/// <summary>
		/// Probability that the mutation takes place
		/// </summary>
		protected internal double probability;

		/// <summary>
		/// Random number generator
		/// </summary>
		protected internal Random generator;

        #endregion

        #region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <remarks>Param values are validated</remarks>
		/// <param name="probability">The mutation probabilty</param>
		/// <param name="generator">Random number generator</param>
		protected MutationAlgorithm(double probability, Random generator)
        {
            String message = String.Empty;

			//Validate the params
			if (ValidateProbability(probability, ref message) == false)
                throw new AIException("probability", this.GetType().ToString(), message);

			if (ValidateGenerator(generator, ref message) == false)
				throw new AIException("generator", this.GetType().ToString(), message);

			this.probability = probability;
			this.generator = generator;
        }

        #endregion

		#region Methods

		/// <summary>
		/// Applies the mutation operator
		/// </summary>
		/// <param name="population">The population we want to mutate</param>
		public abstract void Mutate(Population<T> population);

		#endregion

        #region Validators

		/// <summary>
		/// Validates if the probability value is correct (in the [0, 1] range)
		/// </summary>
		/// <param name="probability">Probability value to validate</param>
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
		/// Validates if the generator is correct (not null)
		/// </summary>
		/// <param name="generator">Generator value to validate</param>
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
