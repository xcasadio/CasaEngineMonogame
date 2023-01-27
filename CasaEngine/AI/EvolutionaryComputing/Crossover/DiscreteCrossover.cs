#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Crossover
{
	/// <summary>
	/// This class represents the discrete crossover algorithm. In this algorithm, one
	/// offspring is create from N parents, and each gene of the offspring is one random
	/// value from the genotype of its parents
	/// </summary>
	/// <typeparam name="T">The type of the genes</typeparam>
	public sealed class DiscreteCrossover<T> : CrossoverAlgorithm<T>
	{
		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="probability"></param>
		/// <param name="generator"></param>
		public DiscreteCrossover(double probability, Random generator)
			: base(probability, generator) {}

        #endregion

		#region Methods

		/// <summary>
		/// Crossover function
		/// </summary>
		/// <param name="parents">The parents to cross</param>
		/// <returns>The list of offsprings</returns>
		public override List<Chromosome<T>> Crossover(List<Chromosome<T>> parents)
		{
			List<Chromosome<T>> list;
			Chromosome<T> chromosome;

			if (parents.Count < 2)
                throw new Exception("The number of parents must be at least 2.");

			list = new List<Chromosome<T>>();

			//Test to see if there´s a crossover or not
			if (base.Crossover(parents) != null)
			{
				list.Add((Chromosome<T>)parents[generator.Next(0, parents.Count - 1)].Clone());

				return list;
			}

			chromosome = parents[0].FastEmptyInstance();

			//Get each gene from the genotype at random from the parents
			for (int i = 0; i < parents[0].Genotype.Count; i++)
				chromosome.Genotype.Add(parents[generator.Next(0, parents.Count - 1)][i]);

			list.Add(chromosome);

			return list;
		}

		#endregion
	}
}
