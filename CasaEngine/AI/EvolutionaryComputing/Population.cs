#region Using directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;




#endregion

namespace CasaEngine.AI.EvolutionaryComputing
{
	/// <summary>
	/// This class represents a population to use in a genetic algorithm or evolutionary strategy. A population is
	/// composed by a list of chromosomes
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	[Serializable]
	public class Population<T> : ICloneable
	{
		#region Fields

		/// <summary>
		/// Genome of the population
		/// </summary>
		protected internal List<Chromosome<T>> genome;

		/// <summary>
		/// Indicates if a perfect solution has been found
		/// </summary>
		protected internal bool hasPerfectSolution;

		/// <summary>
		/// Index of the perfect solution
		/// </summary>
		protected internal int perfectSolutionIndex;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		public Population()
		{
			genome = new List<Chromosome<T>>();
			hasPerfectSolution = false;
			perfectSolutionIndex = -1;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the list of chromosomes of this population
		/// </summary>
		public virtual List<Chromosome<T>> Genome
		{
			get { return genome; }
			set
			{
				String message = String.Empty;

				if (ValidateGenome(value, ref message) == false)
					throw new AIException("genome", this.GetType().ToString(), message);

				genome = value;
			}
		}

		/// <summary>
		/// Gets the total fitness value of the population
		/// </summary>
		public virtual double TotalFitness
		{
			get
			{
				double total = 0;

				for (int i = 0; i < genome.Count; i++)
					total += genome[i].Fitness;

				return total;
			}
		}

		/// <summary>
		/// Gets the average fitness of the population
		/// </summary>
		public virtual double AverageFitness
		{
			get
			{
				return TotalFitness / ((double) genome.Count);
			}
		}

		/// <summary>
		/// Gets or sets if a perfect solution was found
		/// </summary>
		public virtual bool HasPerfectSolution
		{
			get { return hasPerfectSolution; }
			set	{ hasPerfectSolution = value; }
		}

		/// <summary>
		/// Gets or sets the index of the perfect solution
		/// </summary>
		public virtual int PerfectSolutionIndex
		{
			get	{ return perfectSolutionIndex; }
			set { perfectSolutionIndex = value; }
		}

		/// <summary>
		/// Indexer to get or set a chromosome from the population
		/// </summary>
		/// <param name="index">Index we want to access</param>
		/// <returns>The chromosome in the index position</returns>
		public virtual Chromosome<T> this[int index]
		{
			get { return genome[index]; }
			set	{ genome[index] = value; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Creates a clone from this object
		/// </summary>
		/// <returns>The clone of the object</returns>
		public object Clone()
		{
			Population<T> newPopulation;

			newPopulation = (Population<T>) MemberwiseClone();
			for (int i = 0; i < genome.Count; i++)
				newPopulation.Genome.Add((Chromosome<T>) genome[i].Clone());

			return newPopulation;
		}

		/// <summary>
		/// Returns a new instance of a population
		/// </summary>
		/// <remarks>
		/// This way of getting an instance is faster than using reflection
		/// and the Activator method. This instance is the same as if
		/// calling the empty constructor to create a Population
		/// </remarks>
		/// <returns>A new population instance</returns>
		public virtual Population<T> FastEmptyInstance()
		{
			Population<T> clone;

			//Clone the actual chromosome
			clone = (Population<T>) base.MemberwiseClone();

			//Restart the internal fields of the chromosome
			clone.Genome = new List<Chromosome<T>>();
			clone.HasPerfectSolution = false;
			clone.PerfectSolutionIndex = -1;

			return clone;
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the population value is correct (not null and with at least one chromosome in its genome)
		/// </summary>
		/// <param name="population">The population value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		private bool ValidatePopulation(Population<T> population, ref string message)
		{
			if (population == null)
			{
				message = "The population can´t be null";
				return false;
			}

			if (ValidateGenome(population.Genome, ref message) == false)
				return false;

			return true;
		}

		/// <summary>
		/// Validates if the genome value is correct (not null)
		/// </summary>
		/// <param name="genome">The genome to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateGenome(List<Chromosome<T>> genome, ref string message)
		{
			if (genome == null)
			{
				message = "The genome can´t be null.";
				return false;
			}

			return true;
		}

		#endregion
	}
}
