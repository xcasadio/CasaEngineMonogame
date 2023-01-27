#region Using Directives

using System;
using System.Collections.Generic;




#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Crossover
{
	/// <summary>
	/// This class represents the lineal crossover algorithm. This algorithm uses two parents to
	/// create two offsprings. The first offspring genotype is:
	/// genotype =  n*(first parent values) + (1-n)*(second parent values)
	/// The second offspring is the other way around. The interval modifier tells the value of n,
	/// and it´s calculated for every gene (this algorithm is pretty similar to arithmetic crossover, 
	/// but in arithmetic crossover the n value is calculated only once)
	/// </summary>
	/// <remarks>
	/// This operator only works with double genes
	/// </remarks>
	public sealed class LinealCrossover : CrossoverAlgorithm<double>
	{
		#region Fields

		/// <summary>
		/// Interval modifier for the lineal crossover
		/// </summary>
		internal double intervalModifier;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="probability">Probability of crossover</param>
		/// <param name="generator">Random number generator</param>
		/// <param name="intervalModifier">The interval modifier</param>
		public LinealCrossover(double probability, Random generator, double intervalModifier)
			: base(probability, generator)
		{
			String message = String.Empty;

			if (ValidateIntervalModifier(intervalModifier, ref message) == false)
				throw new AIException("intervalModifier", this.GetType().ToString(), message);

			this.intervalModifier = intervalModifier;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Crossover function
		/// </summary>
		/// <param name="parents">The parents to cross</param>
		/// <returns>The list of offsprings</returns>
		public override List<Chromosome<double>> Crossover(List<Chromosome<double>> parents)
		{
			List<Chromosome<double>> list;
			Chromosome<double> chromosome1, chromosome2;
			double scaleFactor = 0;

			//This algorithm uses only 2 parents
			if (parents.Count != 2)
				throw new Exception("The number of parents must be 2.");

			list = new List<Chromosome<double>>();

			//Test to see if there´s a crossover or not
			if (base.Crossover(parents) != null)
			{
				list.Add((Chromosome<double>)parents[0].Clone());
				list.Add((Chromosome<double>)parents[1].Clone());

				return list;
			}

			chromosome1 = parents[0].FastEmptyInstance();
			chromosome2 = parents[0].FastEmptyInstance();

			//Calculate the genotype of each chromosome. The scale factor is calcultated every time
			for (int i = 0; i < parents[0].Genotype.Count; i++)
			{
				scaleFactor = generator.NextDouble() * (1 - 2 * intervalModifier) + intervalModifier;

				chromosome1.Genotype.Add(parents[0][i] * scaleFactor + parents[0][i] * (1 - scaleFactor));
				chromosome2.Genotype.Add(parents[0][i] * (1 - scaleFactor) + parents[0][i] * scaleFactor);
			}

			list.Add(chromosome1);
			list.Add(chromosome2);

			return list;
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the interval modifier value is correct (>= 0)
		/// </summary>
		/// <param name="intervalModifier">The interval modifier value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateIntervalModifier(double intervalModifier, ref string message)
		{
			if (intervalModifier < 0)
			{
				message = "The interval modifier must be equal or greater than 0.";
				return false;
			}

			return true;
		}

		#endregion
	}
}
