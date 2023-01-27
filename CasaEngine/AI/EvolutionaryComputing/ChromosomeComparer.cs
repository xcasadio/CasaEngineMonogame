#region Using directives

using System;
using System.Collections.Generic;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing
{
	/// <summary>
	/// This helper class is used to sort chromosome lists in ascending or descending order by
	/// their fitness value
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public class ChromosomeComparer<T> : IComparer<Chromosome<T>>
	{
		#region Fields

		/// <summary>
		/// Indicates in which order we want to sort the chromosomes
		/// </summary>
		protected internal EvolutionObjective order;

        #endregion

        #region Constructors
		
		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="order">Order to sort the chromosomes</param>
		public ChromosomeComparer(EvolutionObjective order)
        {
            this.order = order;
        }

        #endregion

		#region IComparer<Chromosome<T>> Members

		/// <summary>
		/// Returns the greater of the 2 compared chromosomes based in their fitness
		/// and the evolution objective selected
		/// </summary>
		/// <param name="x">First chromosome to compare</param>
		/// <param name="y">Second chromosome to compare</param>
		/// <returns>Positive number if the first chromosome is greater, negative if it´s the second one, 0 if they are equal</returns>
		public int Compare(Chromosome<T> x, Chromosome<T> y)
		{
			if (order == EvolutionObjective.Minimize)
				return x.Fitness.CompareTo(y.Fitness);

			else
				return -(x.Fitness.CompareTo(y.Fitness));
		}

		#endregion
	}
}
