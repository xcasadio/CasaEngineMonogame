#region Using Directives

using System;
using System.Collections.Generic;


using CasaEngine.AI.EvolutionaryComputing.Crossover;
using CasaEngine.AI.EvolutionaryComputing.Scaling;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Selection
{
	/// <summary>
	/// This class represents the roulette wheel selection operator. This operator sees the whole parents population
	/// as a probability circle and selects individuals from that circle at random based in their scaled fitness
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public sealed class RouletteWheelSelection<T> : SelectionAlgorithm<T>
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
		public RouletteWheelSelection(int numberParents, Random generator, EvolutionObjective objective, CrossoverMethod<T> crossover, ScalingMethod<T> scaling)
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
			double selectedFitness, total;
			List<Chromosome<T>> selectedChromosomes;

			//Select the winner chromosomes
			selectedChromosomes = new List<Chromosome<T>>();
			for (i = 0; i < this.numberParents; i++)
			{
				selectedFitness = generator.NextDouble() * scaledPopulation.TotalFitness;

				//Search for the winner chromosome
				total = 0;
				j = -1;
				while (total < selectedFitness)
				{
					j++;
					total += scaledPopulation.Fitness[j];
				}

				selectedChromosomes.Add((Chromosome<T>) scaledPopulation.Chromosomes[j].Clone());
			}

			return selectedChromosomes;
		}

		#endregion
	}
}
