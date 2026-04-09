namespace CasaEngine.Framework.AI.Algorithms.EvolutionaryComputing.Crossover;

public delegate List<Chromosome<T>> CrossoverMethod<T>(List<Chromosome<T>> parents);