#region Using Directives

using System;
using System.Collections.Generic;


using CasaEngine.AI.EvolutionaryComputing.Crossover;
using CasaEngine.AI.EvolutionaryComputing.Scaling;


#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Selection
{
	/// <summary>
	/// This class represents the tournament selection operator. This operator selects the winner individuals doing
	/// tournaments between N chromosomes. The fittest of those N chromosomes is the winner of the tournament and
	/// it´s selected. This is repeated until all winners are selected.
	/// </summary>
	/// <remarks>This operator doesn´t need a scaling method (so it´s pretty fast)</remarks>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public sealed class TournamentSelection<T> : SelectionAlgorithm<T>
	{
		#region Fields

		/// <summary>
		/// Tournament size
		/// </summary>
		internal int size;

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
		/// <param name="size">Size of the tournament</param>
		public TournamentSelection(int numberParents, Random generator, EvolutionObjective objective, CrossoverMethod<T> crossover, ScalingMethod<T> scaling, int size)
			: base(numberParents, generator, objective, crossover, scaling)
		{
			String message = String.Empty;

			if (ValidateSize(size, ref message) == false)
				throw new AIException("size", this.GetType().ToString(), message);

			this.size = size;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Applies the selection operator
		/// </summary>
		/// <returns>The list of selected chromosomes to create the new children population</returns>
		protected override List<Chromosome<T>> Select()
		{
			List<Chromosome<T>> selectedChromosomes;

			//Select the chromosomes using tournaments
			selectedChromosomes = new List<Chromosome<T>>();
			for (int i = 0; i < this.numberParents; i++)
				selectedChromosomes.Add(Tournament(population));

			return selectedChromosomes;
		}

		/// <summary>
		/// Does a tournament
		/// </summary>
		/// <param name="population">Chromosomes that will take part of the tournament</param>
		/// <returns>The tournament winner based in the evolution objective</returns>
		private Chromosome<T> Tournament(Population<T> population)
		{
			int tournamentTry, selectedChromosome = 0;
			double bestValue;

			if (objective == EvolutionObjective.Maximize)
				bestValue = double.MinValue;

			else
				bestValue = double.MaxValue;

			//Search the winner chromosome
			for (int i = 0; i < size; i++)
			{
				//Select the chromosome that will participate in the tournament
				tournamentTry = generator.Next(0, population.Genome.Count - 1);

				//See if we´ve got a better individual
				if (objective == EvolutionObjective.Maximize)
				{
					if (population[tournamentTry].Fitness > bestValue)
					{
						bestValue = population[tournamentTry].Fitness;
						selectedChromosome = tournamentTry;
					}
				}

				else
				{
					if (population[tournamentTry].Fitness < bestValue)
					{
						bestValue = population[tournamentTry].Fitness;
						selectedChromosome = tournamentTry;
					}
				}
			}

			return (Chromosome<T>) population[selectedChromosome].Clone();
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the tournament size is correct (greater than 1)
		/// </summary>
		/// <param name="size">Tournament size value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public bool ValidateSize(int size, ref String message)
		{
			if (size < 2)
			{
				message = "The size of the tournament must be greater than 1.";
				return false;
			}

			return true;
		}

		#endregion
	}
}
