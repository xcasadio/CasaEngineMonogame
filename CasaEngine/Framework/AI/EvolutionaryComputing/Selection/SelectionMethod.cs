namespace CasaEngine.Framework.AI.EvolutionaryComputing.Selection;

public delegate Population<T> SelectionMethod<T>(Population<T> population, int offspringPopulationSize);