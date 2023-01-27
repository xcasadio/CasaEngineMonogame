#region Using Directives

using System;




#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Replacement
{
	/// <summary>
	/// This class represents the elitism replacement operator. In this type of replacement, the N best parents
	/// will survive in the next generation. The rest of the population will be composed by the best children.
	/// </summary>
	/// <remarks>This type of replacement is not used normally in evolutionary strategies</remarks>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public sealed class ElitismReplacement<T> : ReplacementAlgorithm<T>
	{
		#region Fields

		/// <summary>
		/// Number of the best parents that will survive in the next generation
		/// </summary>
		internal int numberParents;

		#endregion
		
		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="newPopulationSize">The size of the new population the replacement will generate</param>
		/// <param name="objective">Evolutionary objective</param>
		/// <param name="numberParents">Number of best parents that will survive in the next generation</param>
		public ElitismReplacement(int newPopulationSize, EvolutionObjective objective, int numberParents)
			: base(newPopulationSize, objective)
        {
			String message = String.Empty;
			
			//Validate arguments
			if (ValidateNumberParents(newPopulationSize, numberParents, ref message) == false)
				throw new AIException("newPopulationSize", this.GetType().ToString(), message);

			this.numberParents = numberParents;
        }

        #endregion

        #region Methods

		/// <summary>
		/// Applies the replacement operator
		/// </summary>
		/// <param name="parents">The parents population</param>
		/// <param name="children">The children population</param>
		/// <returns>The new population that will replace the parents</returns>
		public override Population<T> Replace(Population<T> parents, Population<T> children)
        {
            Population<T> survivors;

			//Sort parents and children
			parents.Genome.Sort(new ChromosomeComparer<T>(objective));
			children.Genome.Sort(new ChromosomeComparer<T>(objective));

			//Create the survivors population
			survivors = parents.FastEmptyInstance();

			//Copy the elite parents
			survivors.Genome.AddRange(parents.Genome.GetRange(0, numberParents));

			//Copy the best children
			survivors.Genome.AddRange(children.Genome.GetRange(0, newPopulationSize - numberParents));

            return survivors;
        }

        #endregion

		#region Validators

		/// <summary>
		/// Validates if the number of parents that will survive in the next generation is correct
		/// (a positive number smaller than the new population size).
		/// </summary>
		/// <param name="newPopulationSize">The new population size</param>
		/// <param name="numberParents">The number of parents that will survive</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the values are correct. False if they are not</returns>
		public static bool ValidateNumberParents(int newPopulationSize, int numberParents, ref string message)
		{
			if (numberParents < 0)
			{
				message = "Number of selected parents must be at least 0.";
				return false;
			}

			if (numberParents >= newPopulationSize)
			{
				message = "Number of selected parents must be lower than the new population size.";
				return false;
			}

			return true;
		}

		#endregion
	}
}
