#region Using Directives

using System;
using System.Collections.Generic;


using CasaEngine.AI.EvolutionaryComputing.Crossover;
using CasaEngine.AI.EvolutionaryComputing.Scaling;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Selection
{
	/// <summary>
	/// This class represents the stochastic universal sampling selection operator. This operator sees the whole 
	/// parents population as a probability circle and selects individuals from that circle using N evenly spaced
	/// hands based on the scaled fitness of the chromosomes and the whole population
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public sealed class SUSSelection<T> : SelectionAlgorithm<T>
	{
		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="numberParents">Number of parents we are going to select</param>
		/// <param name="generator">Random number generator</param>
		/// <param name="objective">Evolution objective of the selection</param>
		/// <param name="crossover">Crossover method for the selection</param>
		/// <param name="scaling">Scaling method for the selection</param>
		public SUSSelection(int numberParents, Random generator, EvolutionObjective objective, CrossoverMethod<T> crossover, ScalingMethod<T> scaling)
			: base(numberParents, generator, objective, crossover, scaling)
		{ }

		#endregion

		#region Methods

		/// <summary>
		/// Applies the selection operator
		/// </summary>
		/// <returns>The list of selected chromosomes to create the new children population</returns>
		protected override List<Chromosome<T>> Select()
		{
			int i, j;
			double selectedFitness, increment, total;
			List<Chromosome<T>> selectedChromosomes;

			//Calculate the spaced hand
			increment = scaledPopulation.TotalFitness / (double) this.numberParents;

			//Get the start position
			selectedChromosomes = new List<Chromosome<T>>();
			selectedFitness = generator.NextDouble() * increment;

			//Select the winner chromosomes
			total = 0;
			j = 0;
			for (i = 0; i < this.numberParents; i++)
			{
				//Search the selected chromosome
				while (total < selectedFitness)
				{
					j++;
					total += scaledPopulation.Fitness[j];
				}

				selectedChromosomes.Add((Chromosome<T>) scaledPopulation.Chromosomes[j].Clone());

				//Increment the position by the spaced hand size
				selectedFitness += increment;
			}

			return selectedChromosomes;
		}

		#endregion
	}
}
