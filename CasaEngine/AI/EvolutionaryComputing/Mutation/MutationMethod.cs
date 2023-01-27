#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
	/// <summary>
	/// Delegate that represents a mutation method
	/// </summary>
	/// <typeparam name="T">The type of the genes</typeparam>
	/// <param name="population">The population to mutate</param>
	public delegate void MutationMethod<T>(Population<T> population);
}
