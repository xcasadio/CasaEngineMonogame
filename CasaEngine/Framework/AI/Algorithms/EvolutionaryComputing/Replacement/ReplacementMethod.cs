namespace CasaEngine.Framework.AI.Algorithms.EvolutionaryComputing.Replacement;

public delegate Population<T> ReplacementMethod<T>(Population<T> parents, Population<T> children);