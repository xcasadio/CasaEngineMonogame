namespace CasaEngine.AI.EvolutionaryComputing
{
	/// <summary>
	/// Indicates the objective the evolution will try to satisfy when
	/// creating new individuals and populations
	/// </summary>
    public enum EvolutionObjective
    {
		/// <summary>
		/// The objective of the evolution is to minimize the fitness value
		/// </summary>
        Minimize = 0,

		/// <summary>
		/// The objective of the evolution is to maximize the fitness value
		/// </summary>
        Maximize = 1
    }
}
