#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Replacement
{
	/// <summary>
	/// This class represents the children replacement operator. In this type of replacement, the new
	/// population is composed by the best children
	/// </summary>
	/// <remarks>This type of replacement is used normally in genetic algorithms</remarks>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public sealed class ChildrenReplacement<T> : ReplacementAlgorithm<T>
	{
		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="newPopulationSize">The size of the new population the replacement will generate</param>
		/// <param name="objective">The objective of the evolution</param>
		public ChildrenReplacement(int newPopulationSize, EvolutionObjective objective)
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

			//Check the population sizes
			if (newPopulationSize > children.Genome.Count)
				throw new Exception("The new population size can´t be greater than the number of children.");

			//Sort children
			children.Genome.Sort(new ChromosomeComparer<T>(objective));

			//Copy the best children to the new population
			survivors = parents.FastEmptyInstance();
			survivors.Genome.AddRange(children.Genome.GetRange(0, newPopulationSize));
			
            return survivors;
        }

        #endregion
	}
}
