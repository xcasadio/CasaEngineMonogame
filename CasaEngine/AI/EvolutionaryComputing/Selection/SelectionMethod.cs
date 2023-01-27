#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Selection
{
	/// <summary>
	/// Delegate that represents a selection method
	/// </summary>
	/// <typeparam name="T">The type of the genes</typeparam>
	/// <param name="population">The population where the selection will take place</param>
	/// <param name="offspringPopulationSize">The size of the offspring population</param>
	/// <returns>The new offspring population</returns>
	public delegate Population<T> SelectionMethod<T>(Population<T> population, int offspringPopulationSize);
}
