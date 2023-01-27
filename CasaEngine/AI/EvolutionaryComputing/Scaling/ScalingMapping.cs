#region Using directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Scaling
{
	/// <summary>
	/// This class represents a list of [chromosome, fitness] pairs where the 
	/// fitness value is a new number created by a scaling algorithm.
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public class ScalingMapping<T>
	{
		#region Fields

		/// <summary>
		/// Position to add a new mapped chromosome
		/// </summary>
		internal int position;

		/// <summary>
		/// Scaled fitness values
		/// </summary>
		internal double[] fitness;

		/// <summary>
		/// Chromosomes of the mapped population
		/// </summary>
		internal Chromosome<T>[] chromosomes;

		/// <summary>
		/// Minimum scaled fitness of the population
		/// </summary>
		internal double minFitness;

		/// <summary>
		/// Total scaled fitness of the population
		/// </summary>
		internal double totalFitness;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="size">Size of the mapping (population size)</param>
		public ScalingMapping(int size)
		{
			fitness = new double[size];
			chromosomes = new Chromosome<T>[size];

			minFitness = double.MaxValue;
			totalFitness = 0;
			position = 0;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the scaled fitness values
		/// </summary>
		public double[] Fitness
		{
			get { return fitness; }
		}

		/// <summary>
		/// Gets the mapped chromosomes
		/// </summary>
		public Chromosome<T>[] Chromosomes
		{
			get { return chromosomes; }
		}

		/// <summary>
		/// Gets the total fitness of the mapped population
		/// </summary>
		public double TotalFitness
		{
			get { return totalFitness; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Adds a chromosome and its new fitness value to the mapping
		/// </summary>
		/// <param name="chromosome">Chromosome to add</param>
		/// <param name="fitness">Fitness value of the chromosome</param>
		public void AddChromosome(Chromosome<T> chromosome, double fitness)
		{
			//Adds the new values
			this.chromosomes[position] = chromosome;
			this.fitness[position] = fitness;

			//Checks to see if we have a new minimum value
			if (fitness < minFitness)
				minFitness = fitness;

			//Update total fitness and insert position
			totalFitness += fitness;
			position++;
		}

		/// <summary>
		/// Makes all scaled fitness values positive (needed for some selection methods)
		/// </summary>
		public void Normalize()
		{
			if (minFitness < 0)
				for (int i = 0; i < fitness.Length - 1; i++)
					fitness[i] -= minFitness;
		}

		#endregion
	}
}
