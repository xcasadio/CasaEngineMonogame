#region Using Directives

using System;
using System.Collections.Generic;


using CasaEngine.AI.EvolutionaryComputing.Crossover;
using CasaEngine.AI.EvolutionaryComputing.Scaling;


#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Selection
{
	/// <summary>
	/// This abstract class represents a selection algorithm that can be applied in a
	/// genetic algorithm or evolutionary strategy. This class must provide the means
	/// of selecting a list of chromosomes based in their fitness score to be the parents
	/// of the new children generation.
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public abstract class SelectionAlgorithm<T>
	{
		#region Fields

		/// <summary>
		/// Number of parents we are going to select
		/// </summary>
        protected internal int numberParents;

		/// <summary>
		/// Random number generator
		/// </summary>
        protected internal Random generator;

		/// <summary>
		/// Evolution objective of the selection
		/// </summary>
        protected internal EvolutionObjective objective;

		/// <summary>
		/// The crossover algorithm
		/// </summary>
		protected internal CrossoverMethod<T> crossover;

		/// <summary>
		/// The scaling algorithm
		/// </summary>
		protected internal ScalingMethod<T> scaling;

		/// <summary>
		/// The parents to use in the selection process
		/// </summary>
		protected internal Population<T> population;

		/// <summary>
		/// The scaled parents to use in the selection process
		/// </summary>
		protected internal ScalingMapping<T> scaledPopulation;

        #endregion

        #region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="numberParents">Number of parents we are going to select</param>
		/// <param name="generator">Random number generator</param>
		/// <param name="objective">Evolution objective of the selection</param>
		/// <param name="crossover">Crossover method for the selection</param>
		/// <param name="scaling">Scaling method for the selection</param>
        public SelectionAlgorithm(int numberParents, Random generator, EvolutionObjective objective, CrossoverMethod<T> crossover, ScalingMethod<T> scaling)
        {
            String message = String.Empty;

            if (ValidateNumberParents(numberParents, ref message) == false)
				throw new AIException("numberParents", this.GetType().ToString(), message);

            if (ValidateGenerator(generator, ref message) == false)
				throw new AIException("generator", this.GetType().ToString(), message);

			if (ValidateCrossover(crossover, ref message) == false)
				throw new AIException("crossover", this.GetType().ToString(), message);

			this.numberParents = numberParents;
			this.generator = generator;
            this.objective = objective;
			this.crossover = crossover;
			this.scaling = scaling;
        }

        #endregion

        #region Methods

		/// <summary>
		/// Applies the selection operator
		/// </summary>
		/// <param name="population">Population where the selection will take place</param>
		/// <param name="offspringPopulationSize">The size of the offspring population</param>
		/// <returns>The new offspring population</returns>
		public Population<T> Selection(Population<T> population, int offspringPopulationSize)
		{
			Population<T> offsprings;
			List<Chromosome<T>> selected;

			//Save the parents
			this.population = population;

			offsprings = population.FastEmptyInstance();

			//Test if scaling is used
			if (scaling != null)
				scaledPopulation = scaling(population);

			//Generate the offsprings
			while (offsprings.Genome.Count < offspringPopulationSize)
			{
				selected = Select();
				offsprings.Genome.AddRange(crossover(selected));
			}

			//Eliminate the excess offsprings
			if (offsprings.Genome.Count > offspringPopulationSize)
			{
				while (offsprings.Genome.Count > offspringPopulationSize)
					offsprings.Genome.RemoveAt(offsprings.Genome.Count - 1);
			}

			return offsprings;
		}

		/// <summary>
		/// Selects the individuals to cross
		/// </summary>
		/// <returns>The list of chromosomes selected</returns>
		protected abstract List<Chromosome<T>> Select();

        #endregion

		#region Validators

		/// <summary>
		/// Validates if the number of parents is correct (greater than 1)
		/// </summary>
		/// <param name="numberParents">The number of parents value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public bool ValidateNumberParents(int numberParents, ref String message)
		{
			if (numberParents < 2)
			{
				message = "The number of selected parents must be greater than 1.";
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

		/// <summary>
		/// Validates if the crossover value is correct (not null)
		/// </summary>
		/// <param name="crossover">The crossover value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateCrossover(CrossoverMethod<T> crossover, ref string message)
		{
			if (crossover == null)
			{
				message = "The crossover operator can´t be null";
				return false;
			}

			return true;
		}

		#endregion
	}
}
