#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Scaling
{
	/// <summary>
	/// This abstract class represents a scaling algorithm that can be applied to
	/// recalculate the fitness values of a population before selection takes place.
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	public abstract class ScalingAlgorithm<T>
	{
		#region Fields

		/// <summary>
		/// Evolution objective
		/// </summary>
		protected internal EvolutionObjective objective;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="objective">Evolution objective</param>
		protected ScalingAlgorithm(EvolutionObjective objective)
		{
			this.objective = objective;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Applies the scaling operator
		/// </summary>
		/// <param name="population">The population we want to scale</param>
		/// <returns>The scaled mapping of the population</returns>
		public abstract ScalingMapping<T> Scale(Population<T> population);

		#endregion
	}
}
