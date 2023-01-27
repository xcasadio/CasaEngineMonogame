#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Scaling
{
	/// <summary>
	/// This class represents the fitness scaling operator. The new fitness for
	/// each chromosome is based in his fitness score or the inverse (depending 
	/// if the objective is to maximize or minimize)
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public sealed class FitnessScaling<T> : ScalingAlgorithm<T>
	{
		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="objective">Evolution objective</param>
		public FitnessScaling(EvolutionObjective objective)
			: base(objective) {}

		#endregion

		#region Methods

		/// <summary>
		/// Applies the scaling operator
		/// </summary>
		/// <param name="population">The population we want to scale</param>
		/// <returns>The scaled mapping of the population</returns>
		public override ScalingMapping<T> Scale(Population<T> population)
		{
			ScalingMapping<T> mapping;
			double temp;

			//Create a new mapping
			mapping = new ScalingMapping<T>(population.Genome.Count);

			//Populate the mapping with the new fitness values
			for (int i = 0; i < population.Genome.Count; i++)
			{
				if (objective == EvolutionObjective.Maximize)
					temp = population[i].Fitness;

				else
					temp = 1 / population[i].Fitness;

				mapping.AddChromosome(population[i], temp);
			}

			return mapping;
		}

		#endregion
	}
}
