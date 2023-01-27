#region Using directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Scaling
{
	/// <summary>
	/// This delegate represents the scaling operator. The scale operator creates a mapping
	/// from the original population to a new structure were all fitness values are scaled depending
	/// on the operator method.
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	/// <param name="population">Population to scale</param>
	/// <returns>The mapping of the population to the new fitness values</returns>
	public delegate ScalingMapping<T> ScalingMethod<T>(Population<T> population);
}
