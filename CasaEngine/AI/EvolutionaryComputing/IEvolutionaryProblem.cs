#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing
{
	/// <summary>
	/// This interface the minimun data the evolutionary computing algorithms need to know about a problem
	/// to try to solve it
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface IEvolutionaryProblem<T>
    {
        #region Properties

		/// <summary>
		/// Gets or sets the objective of the problem
		/// </summary>
		EvolutionObjective Objective
		{
			get;
			set;
		}

        #endregion

        #region Methods

		/// <summary>
		/// This method generates the initial population of the problem
		/// </summary>
		/// <returns>A new population to try to solve the problem</returns>
		Population<T> GenerateInitialPopulation();

		/// <summary>
		/// Calculates the fitness of the population
		/// </summary>
		/// <param name="population">The population we want to calculate it fitness values</param>
		void CalculateFitness(Population<T> population);

        #endregion
    }
}
