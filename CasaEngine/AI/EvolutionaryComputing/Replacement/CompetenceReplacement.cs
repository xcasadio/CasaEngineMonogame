#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Replacement
{
	/// <summary>
	/// This class represents the competence replacement operator. In this type of replacement, the new
	/// population is composed by the best elements choosen from the parents and children populations
	/// </summary>
	//// <typeparam name="T">The genes type. Can be anything</typeparam>
	public sealed class CompetenceReplacement<T> : ReplacementAlgorithm<T>
	{
        #region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="newPopulationSize">The size of the new population the replacement will generate</param>
		/// <param name="objective">Evolutionary objective</param>
		public CompetenceReplacement(int newPopulationSize, EvolutionObjective objective)
			: base(newPopulationSize, objective) {}

        #endregion

        #region Methods

		/// <summary>
		/// Applies the replacement operator
		/// </summary>
		/// <param name="parents">The parents population</param>
		/// <param name="children">The children population</param>
		/// <returns>The new population that will replace the parents</returns>
		public override Population<T> Replace(Population<T> parents, Population<T> children)
        {
			Population<T> survivors;

			// Mix the parents and the children to create a new population
			survivors = parents.FastEmptyInstance();
			survivors.Genome.AddRange(parents.Genome);
			survivors.Genome.AddRange(children.Genome);

			//Sort the survivors
			survivors.Genome.Sort(new ChromosomeComparer<T>(objective));

			// Remove the less fit chromosomes
			survivors.Genome.RemoveRange(newPopulationSize, survivors.Genome.Count - newPopulationSize);

			return survivors;
        }

        #endregion
	}
}
