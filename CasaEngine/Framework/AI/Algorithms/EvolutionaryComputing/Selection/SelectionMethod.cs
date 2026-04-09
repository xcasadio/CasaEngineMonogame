namespace CasaEngine.Framework.AI.Algorithms.EvolutionaryComputing.Selection;

public delegate Population<T> SelectionMethod<T>(Population<T> population, int offspringPopulationSize);